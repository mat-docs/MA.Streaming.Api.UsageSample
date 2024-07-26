# Stream API Python Example
## Compiling from proto files

Python definitions can be generated from the proto files. 

The latest proto file can be found on [GitHub](https://github.com/Software-Products/MA.DataPlatforms.Protocol).

Python’s gRPC tools include the protocol buffer compiler protoc and the special plugin for generating server and client 
code from .proto service definitions.

```commandline
python -m pip install grpcio-tools
```
A batch script has been included to make this process easier. 
To compile the required Python definitions, clone the repo and update the `path_to_proto` parameter in `gen_proto.bat`.

Run the script at the project root and make sure that the root path is included in the Python path at runtime.

```commandline
gen_proto.bat
```

## Stream Writer Basic

This example demonstrates how to publish new data via the Stream API.

Two parameters are present as part of this sample, `Sin:MyApp`, and `Cos:MyApp`,corresponding
to a sine and cosine wave.

## Stream Reader SQLRace

This example demonstrates how to consume data from the broker via the Stream API.
The example will read packets for an ongoing session, and write the contents to an
ATLAS Session.
If there are no ongoing session, it will wait until a new one starts.
The example will continuously wait for a live session until stopped.
