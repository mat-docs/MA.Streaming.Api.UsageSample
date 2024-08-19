import logging
import multiprocessing
from multiprocessing import Process, Queue, Manager
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
                    (s.value if s.status == open_data_pb2.DataStatus.DATA_STATUS_VALID else np.nan)
                    for s in samples
                ]
                all_timestamps.append(timestamps_sqlrace)
                all_data.append(data)

        shared_data[data_format_identifier] = (all_data, all_timestamps)


class RowPacketProcessor:

    def __init__(self, session_writer, data_format_cache):
        self.packet_queues: Dict[int, Queue] = dict()
        self.session_writer = session_writer
        self.data_format_cache = data_format_cache
        self.queue_threshold = 200
        self.process_interval = 30
        self.processing_queues = False

        # Start the background thread
        self.stop_event = threading.Event()
        self.background_thread = threading.Thread(target=self.schedule_process_queue)
        self.background_thread.start()

    def __exit__(self, exc_type, exc_val, exc_tb):
        self.stop()

    def add_packet_to_queue(self, packet: open_data_pb2.RowDataPacket):
        if packet.data_format.data_format_identifier == 0:
            data_format_identifier = self.data_format_cache.get_cached_data_format_identifier(
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
            time.sleep(self.process_interval)
            self.process_queues(True)
            if len(self.packet_queues) != 0:
                max_queue_length = max([queue.qsize() for key, queue in self.packet_queues.items()])
                while max_queue_length > self.queue_threshold:
                    self.process_queues()
                    max_queue_length = max([queue.qsize() for key, queue in self.packet_queues.items()])

    def process_queues(self, use_all_processors=False):
        if self.processing_queues:
            return
        self.processing_queues = True
        processes = []
        manager = Manager()
        shared_data = manager.dict()

        sorted_queues = sorted(self.packet_queues.items(), key=lambda item: item[1].qsize(), reverse=True)

        max_queue_length = 0
        for data_format_identifier, queue in sorted_queues:
            max_queue_length = queue.qsize()
            if len(processes) >= multiprocessing.cpu_count():
                break
            if queue.qsize() >= self.queue_threshold or use_all_processors:
                parameter_identifiers = self.data_format_cache.get_cached_parameter_list(data_format_identifier)
                pp = PacketProcessor(data_format_identifier, parameter_identifiers, queue, shared_data)
                p = Process(target=pp.start_process())
                p.start()
                processes.append(p)

        logger.info("Processing %i row packet queue. Remaining max queue length %i", len(processes), max_queue_length)

        for p in processes:
            p.join()

        self.processing_queues = False

        if len(shared_data) > 0:
            self.write_to_session(shared_data)

    def write_to_session(self, shared_data):
        for data_format_identifier, (all_data, all_timestamps) in shared_data.items():
            parameter_identifiers = self.data_format_cache.get_cached_parameter_list(data_format_identifier)
            all_data_array = np.array(all_data).transpose()
            assert len(parameter_identifiers) == len(all_data_array)

            for name, column in zip(parameter_identifiers, all_data_array):
                if all(np.isnan(column)):
                    continue
                if not self.session_writer.add_data(
                        str(name),
                        column[~np.isnan(column)].tolist(),
                        np.array(all_timestamps)[~np.isnan(column)].tolist()
                ):
                    logger.warning("Failed to add data for parameter %s", str(name))

            logger.debug("Added row data block of size %s", all_data_array.shape)

    def stop(self):
        self.stop_event.set()
        self.background_thread.join()
        max_queue_length = max([queue.qsize() for key, queue in self.packet_queues.items()])
        while max_queue_length > 0:
            self.process_queues(True)
            max_queue_length = max([queue.qsize() for key, queue in self.packet_queues.items()])
