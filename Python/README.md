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

This is an example demonstrating how to publish data via the stream API. 
Two periodic parameters are created as part of this example, `Sin:MyApp` and `Cos:MyApp` which are sin and cos waves.  
