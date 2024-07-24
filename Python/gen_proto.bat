REM Script to generate Python classes from proto files

SET path_to_proto="C:/MA.DataPlatforms.Protocol/"
python -m grpc_tools.protoc -I %path_to_proto%proto --python_out=. --pyi_out=. --grpc_python_out=. %path_to_proto%proto/ma/streaming/api/v1/api.proto
python -m grpc_tools.protoc -I %path_to_proto%proto --python_out=. --pyi_out=. --grpc_python_out=. %path_to_proto%proto/ma/streaming/open_data/v1/open_data.proto
python -m grpc_tools.protoc -I %path_to_proto%proto --python_out=. --pyi_out=. --grpc_python_out=. %path_to_proto%proto/ma/streaming/key_generator/v1/key_generator.proto