import logging
from functools import cache
from multiprocessing import Process, Queue, Manager, Lock
from typing import List, Dict
import pandas as pd
import numpy as np
import threading
import time

from ma.streaming.api.v1 import api_pb2
from ma.streaming.open_data.v1 import open_data_pb2
from stream_api import StreamApi

logger = logging.getLogger(__name__)


class PacketProcessor():
    def __init__(self, data_format_identifier, parameter_identifiers, queue, shared_data):
        self.data_format_identifier = data_format_identifier
        self.parameter_identifiers = parameter_identifiers
        self.queue = queue
        self.shared_data = shared_data

    def start_process(self):
        packets = []
        while not (self.queue.empty() and len(packets) < 1000):
            packets.append(self.queue.get())

        if packets:
            self.handle_row_packets(self.data_format_identifier, self.parameter_identifiers, packets, self.shared_data)

    def handle_row_packets(self, data_format_identifier: int, parameter_identifiers: List[str],
                           packets: List[open_data_pb2.RowDataPacket], shared_data):
        all_data = []
        all_timestamps = []

        for packet in packets:
            samples = getattr(packet.rows[0], packet.rows[0].WhichOneof("list")).samples
            assert len(parameter_identifiers) == len(
                samples), "The number of parameter identifiers should match the number of columns"
            assert len(packet.timestamps) == len(
                packet.rows), "The number of timestamps should match the number of rows."

            for timestamps_ns, row in zip(packet.timestamps, packet.rows):
                timestamps_sqlrace = np.mod(timestamps_ns, 1e9 * 3600 * 24)
                samples = getattr(row, row.WhichOneof("list")).samples
                data = [
                    (s.value if s.status == open_data_pb2.DataStatus.DATA_STATUS_VALID else pd.NA)
                    for s in samples
                ]
                all_timestamps.append(timestamps_sqlrace)
                all_data.append(data)

        df = pd.DataFrame(all_data, columns=parameter_identifiers, index=all_timestamps, dtype=float)
        shared_data[data_format_identifier] = df


class RowPacketProcessor:

    def __init__(self, session_writer, data_source, grpc_address):
        self.data_source = data_source
        self.packet_queues: Dict[int, Queue] = dict()
        self.session_writer = session_writer
        self.grpc_address = grpc_address
        self.stream_api = StreamApi(self.grpc_address)
        self.queue_threshold = 200
        self.process_interval = 5
        self.processing_queues = False

        # Start the background thread
        self.stop_event = threading.Event()
        self.background_thread = threading.Thread(target=self.schedule_process_queue)
        self.background_thread.start()

    def __exit__(self, exc_type, exc_val, exc_tb):
        self.stop()

    def add_packet_to_queue(self, packet: open_data_pb2.RowDataPacket):
        if packet.data_format.data_format_identifier == 0:
            data_format_identifier = self.get_cached_data_format_identifier(
                packet.data_format.parameter_identifiers.parameter_identifiers)
        else:
            data_format_identifier = packet.data_format.data_format_identifier

        if data_format_identifier not in self.packet_queues:
            self.packet_queues[data_format_identifier] = Queue()

        self.packet_queues[data_format_identifier].put(packet)
        if self.packet_queues[data_format_identifier].qsize() > self.queue_threshold:
            self.process_queues()

    def schedule_process_queue(self):
        while not self.stop_event.is_set():
            self.process_queues()
            time.sleep(self.process_interval)

    def process_queues(self, process_all=False):
        if self.processing_queues:
            return
        self.processing_queues = True
        processes = []
        manager = Manager()
        shared_data = manager.dict()

        for data_format_identifier, queue in self.packet_queues.items():
            if queue.qsize() >= self.queue_threshold or process_all:
                parameter_identifiers = self.get_cached_parameter_list(data_format_identifier)
                pp = PacketProcessor(data_format_identifier, parameter_identifiers, queue, shared_data)
                p = Process(target=pp.start_process())
                p.start()
                processes.append(p)

        logger.info("Processing %i row packet queue", len(processes))

        for p in processes:
            p.join()

        self.processing_queues = False

        # if len(shared_data) > 0:
        self.write_to_session(shared_data)

    def write_to_session(self, shared_data):
        for data_format_identifier,df in shared_data.items():
            parameter_identifiers = self.get_cached_parameter_list(data_format_identifier)
            for name, column in df.items():
                column = column.dropna()
                if column.size == 0:
                    continue
                if not self.session_writer.add_data(str(name), column.values.tolist(), column.index.tolist()):
                    logger.warning("Failed to add data for parameter %s", str(name))

            logger.info("Added row data block of size %s", df.shape)

    @cache
    def get_cached_data_format_identifier(self, parameter_identifiers: List[str]) -> int:
        data_format_manager_stub = self.stream_api.data_format_manager_service_stub
        data_format_identifier_response = data_format_manager_stub.GetParameterDataFormatId(
            api_pb2.GetParameterDataFormatIdRequest(
                data_source=self.data_source,
                parameters=parameter_identifiers
            )
        )
        return data_format_identifier_response.data_format_identifier

    @cache
    def get_cached_parameter_list(self, data_format_identifier: int) -> List[str]:
        data_format_manager_stub = self.stream_api.data_format_manager_service_stub
        param_list_response = data_format_manager_stub.GetParametersList(
            api_pb2.GetParametersListRequest(
                data_source=self.data_source,
                data_format_identifier=data_format_identifier,
            )
        )
        return list(param_list_response.parameters)

    def stop(self):
        self.stop_event.set()
        self.background_thread.join()
        self.process_queues(process_all=True)
