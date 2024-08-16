from functools import cache
from typing import List

from ma.streaming.api.v1 import api_pb2
from stream_api import StreamApi


class DataFormatCache:

    def __init__(self, data_source, grpc_address="localhost:13579"):
        self.data_source = data_source
        self.grpc_address = grpc_address
        self.stream_api = StreamApi(self.grpc_address)

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
