"""Read data packets from broker via the Stream API and write to a ATLAS session

This example demonstrates how to consume data from the broker via the Stream API.
The example will read packets for an ongoing session, and write the contents to an
ATLAS Session.
If there are no ongoing session, it will wait until a new one starts.

See Also:
    stream_writer_basic
"""

import asyncio
import logging
import signal
import threading
from datetime import datetime, timedelta
from collections import deque

import grpc
import numpy as np
from google.protobuf import wrappers_pb2

from ma.streaming.api.v1 import api_pb2_grpc, api_pb2
from ma.streaming.open_data.v1 import open_data_pb2
from stream_api import StreamApi
from atlas_session_writer import AtlasSessionWriter
from stream_reader_sqlrace.data_format_cache import DataFormatCache
from stream_reader_sqlrace.row_packet_processor import RowPacketProcessor

logger = logging.getLogger(__name__)


class StreamReaderSql:
    """Read data from the Stream API and write it to an ATLAS Session"""

    def __init__(self, sqlrace_data_source, sqlrace_database):
        self.last_processed = datetime.now()
        self.packets_to_add = deque()
        self.identifiers_with_missing_config = set()
        self.events_with_missing_config = set()
        self.add_missing_config = True
        self.connection = None
        self.data_source = "SampleDataSource"
        self.grpc_address = "localhost:13579"
        self.stream_api = StreamApi(self.grpc_address)
        self.session_key = None
        self.essentials_iterator = None
        self.packets_iterator = None
        self.main_task = None
        self.read_essentials_task = None
        self.schedule_process_queue_task = None
        self.is_session_complete = False
        self.sqlrace_data_source = sqlrace_data_source
        self.sqlrace_database = sqlrace_database
        self.session_writer: AtlasSessionWriter = None
        self.row_packet_processor: RowPacketProcessor = None
        self.data_format_cache: DataFormatCache = None
        self.packet_queue_limit = 200_000
        self.process_queue_interval = 30
        self.terminate = threading.Event()

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        asyncio.run(self.async_stop())

    async def async_stop(self):
        while len(self.packets_to_add) > 0:
            await self.process_queue()
        if self.row_packet_processor is not None:
            self.row_packet_processor.stop()
        # if the session writer is initialized and the session hasn't been closed yet.
        if self.session_writer is not None and self.session_writer.sql_race_connection is not None:
            session_info_response = self.stream_api.session_management_service_stub.GetSessionInfo(
                api_pb2.GetSessionInfoRequest(session_key=self.session_key)
            )
            self.is_session_complete = session_info_response.is_complete
            self.session_writer.session.UpdateIdentifier(session_info_response.identifier)
            self.session_writer.add_details("Data Source", self.data_source)
            self.session_writer.close_session()
        close_session_response = (
            self.stream_api.connection_manager_service_stub.CloseConnection(
                api_pb2.CloseConnectionRequest(connection=self.connection)
            )
        )
        if close_session_response.success:
            logger.info("Connection closed.")
        else:
            logger.warning("Connection was not successfully closed.")

    async def session_stop(self):
        # If the session is live, subscribe to the session stop notification
        if not self.is_session_complete:
            async with grpc.aio.insecure_channel(self.grpc_address) as channel:
                session_management_stub = api_pb2_grpc.SessionManagementServiceStub(channel)
                async for (
                        stop_notification
                ) in session_management_stub.GetSessionStopNotification(
                    api_pb2.GetSessionStopNotificationRequest(data_source=self.data_source)
                ):
                    logger.debug("Stop notification received for session: %s", stop_notification.session_key)
                    if stop_notification.session_key == self.session_key:
                        break

        while (datetime.now() - self.last_processed < timedelta(seconds=self.process_queue_interval+5)) or (len(self.packets_to_add) > 0):
            await asyncio.sleep(self.process_queue_interval+10)
        logger.info("Finished processing remaining packets, terminating...")
        self.terminate_main_task()

    def terminate_main_task(self, *_):
        logger.info("Terminating main task.")
        self.essentials_iterator.cancel()
        self.packets_iterator.cancel()
        # self.main_task.cancel()
        # self.read_essentials_task.cancel()
        self.terminate.set()

    async def read_essentials(self):
        logger.info("Start reading essential packets")
        async with grpc.aio.insecure_channel(self.grpc_address) as channel:
            packet_reader_stub = api_pb2_grpc.PacketReaderServiceStub(channel)
            essentials_iterator = packet_reader_stub.ReadEssentials(
                    api_pb2.ReadEssentialsRequest(connection=self.connection)
            )
            self.essentials_iterator = essentials_iterator
            async for essentials_packet_response in essentials_iterator:
                logger.debug("New essential packet received.")
                await asyncio.gather(*[self.deserialize_new_packet(response.packet, True) for response in essentials_packet_response.response])
                await self.process_queue()

    async def read_packets(self):
        logger.info("Start reading packets")
        options = [('grpc.max_message_length', 100 * 1024 * 1024)]
        async with grpc.aio.insecure_channel(self.grpc_address, options=options) as channel:
            packet_reader_stub = api_pb2_grpc.PacketReaderServiceStub(channel)
            packets_iterator = packet_reader_stub.ReadPackets(
                api_pb2.ReadPacketsRequest(connection=self.connection)
            )
            self.packets_iterator = packets_iterator
            async for new_packet_response in packets_iterator:
                logger.debug("New packet received.")
                await asyncio.gather(*[self.deserialize_new_packet(response.packet, True) for response in new_packet_response.response])
                if len(self.packets_to_add) > self.packet_queue_limit:
                    # back off reading packets if we can't process it fast enough
                    await asyncio.sleep(len(self.packets_to_add)/self.packet_queue_limit)

    async def handle_packet_missing_config(self, packet, parameter_identifiers):
        """Process the packet for missing config.

        Args:
            packet: packet
            parameter_identifiers: list of parameter identifiers within the packet

        Returns:
            True if there is missing config.
        """
        missing_config = False
        for parameter_identifier in parameter_identifiers:
            if len(parameter_identifier.split(":")) == 1:
                parameter_identifier += ":StreamAPI"
            if parameter_identifier not in self.session_writer.parameter_channel_id_mapping.keys():
                self.identifiers_with_missing_config.add(parameter_identifier)
                missing_config = True

        if missing_config:
            self.packets_to_add.appendleft(packet)
            logger.debug("Missing config packet added to queue")

        return missing_config

    async def handle_event_packet_missing_config(self, packet: open_data_pb2.EventPacket, event_identifier: str):
        """Process the packet for missing config.

        Args:
            packet: packet
            event_identifier: event identifier within the packet

        Returns:
            True if there is missing config.
        """
        missing_config = False
        if event_identifier not in self.session_writer.event_identifier_mapping.keys():
            self.events_with_missing_config.add(event_identifier)
            missing_config = True

        if missing_config:
            self.packets_to_add.appendleft(packet)
            logger.debug("Missing config packet added to queue")

        return missing_config

    async def schedule_process_queue(self):
        while True:  # Terminated by setting terminate
            await asyncio.sleep(self.process_queue_interval)
            await self.process_queue()
            while len(self.packets_to_add) > self.packet_queue_limit:
                await self.process_queue()
            if self.terminate.is_set():
                break

    async def process_queue(self):
        self.session_writer.add_missing_configration(
            list(self.identifiers_with_missing_config.copy()),
            list(self.events_with_missing_config.copy())
        )
        self.identifiers_with_missing_config.clear()
        self.events_with_missing_config.clear()

        packets = list(self.packets_to_add)
        self.packets_to_add.clear()
        self.packets_to_add.extendleft(packets[self.packet_queue_limit:][::-1])
        packets = packets[:self.packet_queue_limit]

        logger.info("Processing %i packets from queue. Remaining queue length: %i", len(packets),
                    len(self.packets_to_add))

        await asyncio.gather(*[self.route_new_packet(packets) for packets in packets])

    async def deserialize_new_packet(
            self, new_packet: open_data_pb2.Packet, match_session_key: bool = False
    ):
        """Decodes new protobuf packets received from the Stream API.

        Args:
            new_packet: Protobuf packet from the open format specification
            match_session_key: True if we only handle packet that match `self.session_key`
        """

        packet_type = new_packet.type
        content = new_packet.content
        session_key = new_packet.session_key

        self.last_processed = datetime.now()

        # discard the packet if the session key does not match
        if match_session_key and (session_key != self.session_key):
            logger.info("Session key mismatch, packet discarded.")
            return

        # try and deserializes the packet, if the packet type can be located.
        # if we can't find a corresponding packet type, discard the packet.
        try:
            packet_class = getattr(open_data_pb2, packet_type + "Packet")
            packet = packet_class.FromString(content)
        except AttributeError:
            logger.debug(
                "Unable to deserializes packet content for pack type %s", packet_type
            )
            return

        self.packets_to_add.append(packet)

    async def route_new_packet(self, packet):
        self.last_processed = datetime.now()
        if isinstance(packet, open_data_pb2.PeriodicDataPacket):
            logger.debug("Periodic packet received.")
            await self.handle_periodic_packet(packet)
        elif isinstance(packet, open_data_pb2.RowDataPacket):
            logger.debug("Row packet received.")
            await self.handle_row_packet(packet)
        elif isinstance(packet, open_data_pb2.EventPacket):
            logger.debug("Event packet received.")
            await self.handle_event_packet(packet)
        elif isinstance(packet, open_data_pb2.MarkerPacket):
            logger.debug("Marker packet received.")
            await self.handle_marker_packet(packet)
        elif isinstance(packet, open_data_pb2.MetadataPacket):
            logger.debug("Metadata packet received.")
            await self.handle_metatdata_packet(packet)
        elif isinstance(packet, open_data_pb2.ConfigurationPacket):
            logger.debug("Config packet received.")
            await self.handle_configuration_packet(packet)
        else:
            logger.info("Unknown packet type, discarded packet %s", packet.__class__)

    async def handle_configuration_packet(
            self, packet: open_data_pb2.ConfigurationPacket
    ):
        # Create a corresponding config in atlas
        self.session_writer.add_configration(packet)

    async def handle_periodic_packet(self, packet: open_data_pb2.PeriodicDataPacket):
        ##  Get the parameter identifier
        if packet.data_format.data_format_identifier != 0:
            data_format_identifier = packet.data_format.data_format_identifier
            parameter_identifiers = self.data_format_cache.get_cached_parameter_list(data_format_identifier)
        else:
            parameter_identifiers = (
                packet.data_format.parameter_identifiers.parameter_identifiers
            )

        assert len(parameter_identifiers) == len(
            packet.columns
        ), "The number of parameter identifiers should match the number of columns"

        # add config if there are no config for the parameters
        if self.add_missing_config:
            if await self.handle_packet_missing_config(packet, parameter_identifiers):
                # If there are missing config then the packet is added to the queue and
                # we return early
                return

        ## Get the periodic data
        start_time = packet.start_time
        interval = packet.interval
        data = []
        status = []
        for column, parameter_identifier in zip(packet.columns, parameter_identifiers):
            samples = getattr(column, column.WhichOneof("list")).samples
            data.append([s.value for s in samples])
            status.append([s.status for s in samples])

        ## Add the data to the session
        # Create timestamps and convert them to SQLRace format.
        timestamps_ns = [start_time + interval * i for i in range(len(data[0]))]
        timestamps_sqlrace = np.mod(timestamps_ns, np.int64(1e9 * 3600 * 24))
        # add the data to the session
        for parameter_identifier, data_for_param in zip(parameter_identifiers, data):
            if not await asyncio.to_thread(self.session_writer.add_data,
                                           parameter_identifier, data_for_param, timestamps_sqlrace
                                           ):
                logger.warning("Failed to add data for parameter %s", parameter_identifier)

    async def handle_row_packet(self, packet: open_data_pb2.RowDataPacket):
        ##  Get the parameter identifier
        if packet.data_format.data_format_identifier != 0:
            data_format_identifier = packet.data_format.data_format_identifier
            parameter_identifiers = self.data_format_cache.get_cached_parameter_list(data_format_identifier)
        else:
            parameter_identifiers = (
                packet.data_format.parameter_identifiers.parameter_identifiers
            )

        samples = getattr(packet.rows[0], packet.rows[0].WhichOneof("list")).samples
        assert len(parameter_identifiers) == len(
            samples
        ), "The number of parameter identifiers should match the number of columns"

        assert len(packet.timestamps) == len(packet.rows), (
            "The number of timestamps" "should match the number of rows."
        )

        # add config if there are no config for the parameters
        if self.add_missing_config:
            if await self.handle_packet_missing_config(packet, parameter_identifiers):
                # If there are missing config then the packet is added to the queue and
                # we return early
                return

        self.row_packet_processor.add_packet_to_queue(packet)

    async def handle_marker_packet(self, packet: open_data_pb2.MarkerPacket):
        if packet.type == "Lap Trigger":
            timestamps_ns = packet.timestamp
            timestamps_sqlrace = np.mod(timestamps_ns, np.int64(1e9 * 3600 * 24))
            self.session_writer.add_lap(timestamps_sqlrace, packet.value, packet.label)
        else:
            timestamps_ns = packet.timestamp
            timestamps_sqlrace = np.mod(timestamps_ns, np.int64(1e9 * 3600 * 24))
            self.session_writer.add_marker(timestamps_sqlrace, packet.label)

    async def handle_metatdata_packet(self, packet: open_data_pb2.MetadataPacket):
        for key, any_value in packet.metadata.items():
            # Check if the value is of type StringValue
            if any_value.Is(wrappers_pb2.StringValue.DESCRIPTOR):
                value = wrappers_pb2.StringValue()
                any_value.Unpack(value)
                self.session_writer.add_details(key, value.value)
            elif any_value.Is(wrappers_pb2.DoubleValue.DESCRIPTOR):
                value = wrappers_pb2.DoubleValue()
                any_value.Unpack(value)
                self.session_writer.add_details(key, value.value)
            else:
                print(f"Unsupported value type for metadata: {key}")

    async def handle_event_packet(self, packet: open_data_pb2.EventPacket):
        # TODO: deal with missing configs
        if packet.data_format.data_format_identifier != 0:
            data_format_manager_stub = self.stream_api.data_format_manager_service_stub
            event_identifier_response = data_format_manager_stub.GetEvent(
                api_pb2.GetEventRequest(
                    data_source=self.data_source,
                    data_format_identifier=packet.data_format.data_format_identifier,
                )
            )
            event_identifier = event_identifier_response.event
        else:
            event_identifier = packet.data_format.event_identifier

        # add config if there are no config for the parameters
        if self.add_missing_config:
            if await self.handle_event_packet_missing_config(packet, event_identifier):
                # If there are missing config then the packet is added to the queue and
                # we return early
                return
        timestamps_ns = packet.timestamp
        timestamps_sqlrace = np.mod(timestamps_ns, np.int64(1e9 * 3600 * 24))
        if not await asyncio.to_thread(self.session_writer.add_event_data,
                                       event_identifier, timestamps_sqlrace, packet.raw_values
                                       ):
            logger.warning("Failed to add event %s", event_identifier)

    async def main(self):
        # This stream reader will continuously wait for a live session until it is stopped.
        # Create the gRPC clients
        connection_management_stub = self.stream_api.connection_manager_service_stub
        session_management_stub = self.stream_api.session_management_service_stub

        # TODO: update log messages now that we can feed in session key directly.
        # Set up a handler if we want to terminate early by ctrl+c
        signal.signal(signal.SIGINT, self.terminate_main_task)
        # Get the latest live session
        current_session_response = session_management_stub.GetCurrentSessions(
            api_pb2.GetCurrentSessionsRequest(data_source=self.data_source)
        )

        for test_key in current_session_response.session_keys[::-1]:
            session_info_response = session_management_stub.GetSessionInfo(
                api_pb2.GetSessionInfoRequest(session_key=test_key)
            )
            if not session_info_response.is_complete:
                self.session_key = test_key
                logger.info("Identified live session %s", self.session_key)
                break

        # if there is no live session wait for a new one to start
        if self.session_key is None:
            logger.info("No live session found, waiting for new session to start.")
            for new_session in session_management_stub.GetSessionStartNotification(
                    api_pb2.GetSessionStartNotificationRequest(data_source=self.data_source)
            ):
                self.session_key = new_session.session_key
                logger.info("Identified live session %s", self.session_key)
                break

        session_info_response = session_management_stub.GetSessionInfo(
            api_pb2.GetSessionInfoRequest(session_key=self.session_key)
        )
        self.is_session_complete = session_info_response.is_complete
        while session_info_response.identifier == '':
            session_info_response = session_management_stub.GetSessionInfo(
                api_pb2.GetSessionInfoRequest(session_key=self.session_key)
            )
            self.is_session_complete = session_info_response.is_complete

        # Establish a new connection
        connection_details = api_pb2.ConnectionDetails(
            data_source=self.data_source,
            session=self.session_key,
            main_offset=session_info_response.main_offset,
            essentials_offset=session_info_response.essentials_offset,
        )

        connection_response = connection_management_stub.NewConnection(
            api_pb2.NewConnectionRequest(details=connection_details)
        )

        self.connection = connection_response.connection

        # Create a corresponding ATLAS session
        self.session_writer = AtlasSessionWriter(self.sqlrace_data_source, self.sqlrace_database,
                                                 session_info_response.identifier)
        self.data_format_cache = DataFormatCache(self.data_source, self.grpc_address)
        self.row_packet_processor = RowPacketProcessor(self.session_writer, self.data_format_cache)

        # Read essential stream which contains essential information such as configs
        self.read_essentials_task = asyncio.create_task(
            self.read_essentials()
        )

        # Read packets and monitor session stop at the same time
        self.main_task = asyncio.gather(
            self.session_stop(),
            self.read_packets(),
        )
        self.schedule_process_queue_task = asyncio.create_task(self.schedule_process_queue())
        logger.debug("Starting main task.")
        await self.main_task


if __name__ == "__main__":
    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(thread)d  %(levelname)s %(name)s %(message)s")

    data_source = r"MCLA-5JRZTQ3\LOCAL"
    database = "SQLRACE01"
    with StreamReaderSql(data_source, database) as stream_recorder:
        stream_recorder.data_source = "Default"
        asyncio.run(stream_recorder.main())
