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

import grpc
import numpy as np

from ma.streaming.api.v1 import api_pb2_grpc, api_pb2
from ma.streaming.open_data.v1 import open_data_pb2
from stream_api import StreamApi
from atlas_session_writer import AtlasSessionWriter

logger = logging.getLogger(__name__)


class StreamReaderSql:
    """Read data from the Stream API and write it to an ATLAS Session"""

    def __init__(self, atlas_ssndb_location: str):
        self.session_writer: AtlasSessionWriter
        self.data_source = "SampleDataSource"
        self.grpc_address = "localhost:13579"
        self.stream_api = StreamApi(self.grpc_address)
        self.session_key = None
        self.main_task = None
        self.atlas_ssndb_location = atlas_ssndb_location

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        self.session_writer.close_session()
        close_session_response = (
            self.stream_api.connection_manager_service_stub.CloseConnection(
                api_pb2.CloseConnectionRequest(connection=self.connection)
            )
        )
        if close_session_response.success:
            logger.info("Connection closed.")
        else:
            logger.warning("Connection was not successfully closed. ")

    async def session_stop(self):
        async with grpc.aio.insecure_channel(self.grpc_address) as channel:
            session_management_stub = api_pb2_grpc.SessionManagementServiceStub(channel)
            async for (
                stop_notification
            ) in session_management_stub.GetSessionStopNotification(
                api_pb2.GetSessionStopNotificationRequest(data_source=self.data_source)
            ):
                print(stop_notification.session_key)
                self.terminate_main_task()

    def terminate_main_task(self, *_):
        logger.info("Terminating main task.")
        self.main_task.cancel()

    async def read_essentials(self):
        async with grpc.aio.insecure_channel(self.grpc_address) as channel:
            packet_reader_stub = api_pb2_grpc.PacketReaderServiceStub(channel)
            async for essentials_packet_response in packet_reader_stub.ReadEssentials(
                api_pb2.ReadEssentialsRequest(connection=self.connection)
            ):
                logger.debug("New essential packet received.")
                new_packet = essentials_packet_response.response[0].packet
                await self.handle_new_packet(new_packet)

    async def read_packets(self):
        async with grpc.aio.insecure_channel(self.grpc_address) as channel:
            packet_reader_stub = api_pb2_grpc.PacketReaderServiceStub(channel)
            async for new_packet_response in packet_reader_stub.ReadPackets(
                api_pb2.ReadPacketsRequest(connection=self.connection)
            ):
                logger.debug("New packet received.")
                new_packet = new_packet_response.response[0].packet
                await self.handle_new_packet(new_packet, True)

    async def handle_new_packet(
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

        # discard the packet if the session key does not match
        if match_session_key and (session_key != self.session_key):
            logger.info("Session key mismatch, packet discarded.")
            return

        # try and deserializes the packet, if the pack type can be located.
        # if we can't find a corresponding packet type, discard the packet.
        try:
            packet_class = getattr(open_data_pb2, packet_type + "Packet")
            packet = packet_class.FromString(content)
        except AttributeError:
            logger.debug(
                "Unable to deserializes pack content for pack type %s", packet_type
            )
            return

        if isinstance(packet, open_data_pb2.PeriodicDataPacket):
            logger.debug("Data packet received.")
            await self.handle_periodic_packet(packet)
        elif isinstance(packet, open_data_pb2.ConfigurationPacket):
            logger.debug("Config packet received.")
            await self.handle_configuration_packet(packet)

    async def handle_configuration_packet(
        self, packet: open_data_pb2.ConfigurationPacket
    ):
        # Create a corresponding config in atlas
        self.session_writer.add_configration(packet)

    async def handle_periodic_packet(self, packet: open_data_pb2.PeriodicDataPacket):
        ##  Get the parameter identifier
        if packet.data_format.data_format_identifier != 0:
            data_format_manager_stub = self.stream_api.data_format_manager_service_stub
            param_list_response = data_format_manager_stub.GetParametersList(
                api_pb2.GetParametersListRequest(
                    data_source=self.data_source,
                    data_format_identifier=packet.data_format.data_format_identifier,
                )
            )
            parameter_identifiers = param_list_response.parameters
        else:
            parameter_identifiers = (
                packet.data_format.parameter_identifiers.parameter_identifiers
            )

        assert len(parameter_identifiers) == len(
            packet.columns
        ), "The number of parameter identifiers should match the number of columns"
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
        timestamps_sqlrace = np.mod(timestamps_ns, 1e9 * 3600 * 24)
        # add the data to the session
        for parameter_identifier, data_for_param in zip(parameter_identifiers, data):
            if not self.session_writer.add_data(
                parameter_identifier, data_for_param, timestamps_sqlrace
            ):
                logger.warning("Failed to add data.")

    async def main(self):
        # Create the gRPC clients
        connection_management_stub = self.stream_api.connection_manager_service_stub
        session_management_stub = self.stream_api.session_management_service_stub

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
        self.session_writer = AtlasSessionWriter(self.atlas_ssndb_location)

        # Read essential stream which contains essential information such as configs
        await self.read_essentials()

        # Set up a handler if we want to terminate early by ctrl+c
        signal.signal(signal.SIGINT, self.terminate_main_task)

        # Read packets and monitor session stop at the same time
        self.main_task = asyncio.gather(
            self.session_stop(),
            self.read_packets(),
        )
        try:
            logger.debug("Starting main task.")
            await self.main_task
        except asyncio.CancelledError:
            logger.info("Terminating...")


if __name__ == "__main__":
    logging.basicConfig(level=logging.DEBUG)

    db_location = r"C:\McLaren Applied\StreamAPIDemo.ssndb"
    with StreamReaderSql(db_location) as stream_recorder:
        asyncio.run(stream_recorder.main())
