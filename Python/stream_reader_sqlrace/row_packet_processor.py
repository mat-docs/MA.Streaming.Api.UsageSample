import logging
from multiprocessing import Queue, Manager
from typing import List, Dict
import threading
import time
import numpy as np

from ma.streaming.open_data.v1 import open_data_pb2

logger = logging.getLogger(__name__)


class RowPacketProcessor:

    def __init__(self, session_writer, data_format_cache):
        self.packet_queues: Dict[int, Queue] = {}
        self.session_writer = session_writer
        self.data_format_cache = data_format_cache
        self.queue_threshold = 50
        self.process_interval = 30
        self.processing_queues = False

        # Start the background thread
        self.stop_event = threading.Event()
        self.background_thread = threading.Thread(
            target=self.schedule_process_queue, name="process_row_thread"
        )
        self.background_thread.start()

    @property
    def max_queue_length(self):
        if len(self.packet_queues) > 0:
            return max(queue.qsize() for (key, queue) in self.packet_queues.items())
        return 0

    def add_packet_to_queue(self, packet: open_data_pb2.RowDataPacket):
        if packet.data_format.data_format_identifier == 0:
            data_format_identifier = (
                self.data_format_cache.get_cached_data_format_identifier(
                    packet.data_format.parameter_identifiers.parameter_identifiers
                )
            )
        else:
            data_format_identifier = packet.data_format.data_format_identifier

        if data_format_identifier not in self.packet_queues:
            q = Queue()
            self.packet_queues[data_format_identifier] = q

        self.packet_queues[data_format_identifier].put(packet)
        if self.packet_queues[data_format_identifier].qsize() > self.queue_threshold:
            self.process_queues()

    def schedule_process_queue(self):
        while not self.stop_event.is_set():
            time.sleep(self.process_interval)
            self.process_queues()
            if len(self.packet_queues) != 0:
                while self.max_queue_length > self.queue_threshold:
                    self.process_queues()

    def process_queues(self, process_all_packets=False):
        if self.processing_queues:
            return
        self.processing_queues = True
        manager = Manager()
        shared_data = manager.dict()

        sorted_queues = sorted(
            self.packet_queues.items(), key=lambda item: item[1].qsize(), reverse=True
        )

        in_process_count = 0
        start_time = time.time()
        for data_format_identifier, queue in sorted_queues:
            if (
                not process_all_packets
                and time.time() - start_time > self.process_interval
            ):
                logger.debug("Terminating early due to timeout.")
                break
            parameter_identifiers = self.data_format_cache.get_cached_parameter_list(
                data_format_identifier
            )
            packets = []
            while not (queue.empty() and len(packets) < 1000):
                packets.append(queue.get())

            for packet in packets:
                samples = getattr(
                    packet.rows[0], packet.rows[0].WhichOneof("list")
                ).samples
                assert len(parameter_identifiers) == len(
                    samples
                ), "The number of parameter identifiers should match the number of columns"
                assert len(packet.timestamps) == len(
                    packet.rows
                ), "The number of timestamps should match the number of rows."

                for timestamps_ns, row in zip(packet.timestamps, packet.rows):
                    samples = getattr(row, row.WhichOneof("list")).samples
                    data = [
                        (
                            s.value
                            if s.status == open_data_pb2.DataStatus.DATA_STATUS_VALID
                            else np.nan
                        )
                        for s in samples
                    ]
                    if not self.session_writer.add_row(
                        parameter_identifiers, data, timestamps_ns
                    ):
                        logger.warning(
                            "Failed to add data for parameters %s",
                            parameter_identifiers,
                        )
            in_process_count += 1

        logger.info(
            "Processing %i out of %i row packet queues. Remaining max queue length %i",
            in_process_count,
            len(sorted_queues),
            self.max_queue_length,
        )

        self.processing_queues = False

    def stop(self):
        self.stop_event.set()
        self.background_thread.join()
        while self.max_queue_length > 0:
            self.process_queues(True)
