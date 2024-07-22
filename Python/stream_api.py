"""All the Stream API services organised in a single class."""

import grpc

from ma.streaming.api.v1 import api_pb2_grpc


class StreamApi:
    """All the Stream API services organised in a single class."""

    def __init__(self, address="localhost:13579"):
        self.channel = grpc.insecure_channel(address)

        # Created the gRPC clients
        self.connection_manager_service_stub = (
            api_pb2_grpc.ConnectionManagerServiceStub(self.channel)
        )
        self.session_management_service_stub = (
            api_pb2_grpc.SessionManagementServiceStub(self.channel)
        )
        self.data_format_manager_service_stub = (
            api_pb2_grpc.DataFormatManagerServiceStub(self.channel)
        )
        self.packet_reader_service_stub = api_pb2_grpc.PacketReaderServiceStub(
            self.channel
        )
        self.packet_writer_service_stub = api_pb2_grpc.PacketWriterServiceStub(
            self.channel
        )
