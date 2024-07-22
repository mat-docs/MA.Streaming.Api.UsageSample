"""Basic example to write data to broker via Stream API.

This example contains a sample implementation of a typical producer.

Two parameters present as part of this sample `Sin:MyApp`, and `Cos:MyApp`,corresponding
to a sine and cosine wave.
Data are generated by the SinWaveGenerator class, and are published in 1 second chunks.

See Also:
    stream_reader_sqlrace.py
"""

import time
import logging

import grpc

from ma.streaming.api.v1 import api_pb2
from ma.streaming.open_data.v1 import open_data_pb2
from ma.streaming.key_generator.v1 import key_generator_pb2_grpc, key_generator_pb2
from packet_builder import SinWaveGenerator
from stream_api import StreamApi

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

# Configurations
DATA_SOURCE = "SampleDataSource"
STREAM = "Stream1"
KEY_GENERATOR_SERVER_ADDRESS = "localhost:15379"


def get_packet_type(packet_content) -> str:
    """Get the string to specify open_data.Packet.type"""

    packet_type = packet_content.__class__.__name__
    # if the packet type ends with packet, we strip the trailing packet, as specified by
    # the open format specification.
    if packet_type[-6:] == "Packet":
        packet_type = packet_type[:-6]
    return packet_type


def get_new_key_string():
    """Get a new string key from the key generator service."""

    channel = grpc.insecure_channel(KEY_GENERATOR_SERVER_ADDRESS)
    key_gen_stub = key_generator_pb2_grpc.UniqueKeyGeneratorServiceStub(channel)
    key_gen_response = key_gen_stub.GenerateUniqueKey(
        key_generator_pb2.GenerateUniqueKeyRequest(
            type=key_generator_pb2.KEY_TYPE_STRING
        )
    )
    return key_gen_response.string_key


def main():
    stream_api = StreamApi()
    sin_wave_generator = SinWaveGenerator()
    session_stub = stream_api.session_management_service_stub
    data_format_stub = stream_api.data_format_manager_service_stub
    packet_writer_stub = stream_api.packet_writer_service_stub

    # Create a new session
    create_session_response = session_stub.CreateSession(
        api_pb2.CreateSessionRequest(
            data_source=DATA_SOURCE,
        )
    )
    session_key = create_session_response.session_key
    logger.info("New session created. Session key: %s", session_key)

    # Query the session info for the session we just create,
    # if we query too fast then it may not be published to the broker yet
    time.sleep(1)
    session_info_response = session_stub.GetSessionInfo(
        api_pb2.GetSessionInfoRequest(
            session_key=session_key,
        )
    )
    print(f"DataSource: {session_info_response.data_source}")
    print(f"Identifier: {session_info_response.identifier}")
    print(f"Type: {session_info_response.type}")
    print(f"Version: {session_info_response.version}")
    print(f"is_complete: {session_info_response.is_complete}")
    try:
        # Build and publish the configuration packet
        config_id = get_new_key_string()
        config_packet = sin_wave_generator.build_configuration_packet(
            config_id=config_id
        )
        packet_type = get_packet_type(config_packet)
        message = open_data_pb2.Packet(
            type=packet_type,
            session_key=session_key,
            is_essential=True,
            content=config_packet.SerializeToString(),
        )
        packet_writer_stub.WriteDataPacket(
            request=api_pb2.WriteDataPacketRequest(
                detail=api_pb2.DataPacketDetails(
                    message=message,
                    data_source=DATA_SOURCE,
                    stream=STREAM,
                    session_key=session_key,
                )
            )
        )
        logger.info("Configuration packet published.")

        # Generate the data format id, which corresponds to the list of parameter
        # identifiers.
        data_format_id_response = data_format_stub.GetParameterDataFormatId(
            request=api_pb2.GetParameterDataFormatIdRequest(
                data_source=DATA_SOURCE, parameters=sin_wave_generator.param_identifiers
            )
        )

        # Build the data packets and publish them via the Stream API
        for i in range(100):
            packet_content = sin_wave_generator.get_new_packet(
                data_format_id_response.data_format_identifier
            )
            packet_type = get_packet_type(packet_content)
            message = open_data_pb2.Packet(
                type=packet_type,
                session_key=session_key,
                is_essential=False,
                content=packet_content.SerializeToString(),
            )
            packet_writer_stub.WriteDataPacket(
                request=api_pb2.WriteDataPacketRequest(
                    detail=api_pb2.DataPacketDetails(
                        message=message,
                        data_source=DATA_SOURCE,
                        stream=STREAM,
                        session_key=session_key,
                    )
                )
            )
            logger.info("Data packet published.")
            time.sleep(1)
            if i % 20 == 0:
                lap_packet = open_data_pb2.MarkerPacket(
                    timestamp=sin_wave_generator.time_ns,
                    label="lap",
                    type="Lap Trigger",
                )
                packet_type = get_packet_type(lap_packet)
                message = open_data_pb2.Packet(
                    type=packet_type,
                    session_key=session_key,
                    is_essential=False,
                    content=lap_packet.SerializeToString(),
                )
                packet_writer_stub.WriteDataPacket(
                    request=api_pb2.WriteDataPacketRequest(
                        detail=api_pb2.DataPacketDetails(
                            message=message,
                            data_source=DATA_SOURCE,
                            stream=STREAM,
                            session_key=session_key,
                        )
                    )
                )
                logger.info("Lap packet published.")
    finally:
        # Close the session regardless
        session_stub.EndSession(
            api_pb2.EndSessionRequest(data_source=DATA_SOURCE, session_key=session_key)
        )
        logger.info("Session ended.")


if __name__ == "__main__":
    main()
