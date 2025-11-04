<img src="/images/malogo.png" width="300" align="right" /><br><br><br>

# Stream API Sample Code
Collection of example code and best practices to use the Stream API.

Stream API is a standard API to expose streaming data from SECU units and abstract away proprietary implementation details. Consumers of this API will be able to write software that interfaces with any viewer, network protocol, or storage technology of their choice. All the data from the SECU unit (after RDA filtering) will be available on the stream. This will be integrated with a protocol for streaming engineering (calibrated) telemetry, interoperating with ATLAS clients and the surrounding data processing ecosystem.

Stream API is responsible for publishing and consuming data from the Kafka broker. Created under the gRPC framework, it allows clients to communicate to a server which handles the Kafka communication. 

It is language and platform agnostic. However, Nuget packages for the C# implementation is provided to registered users in the [Motion Applied Nuget Repository](https://github.com/mat-docs/packages). Proto files are provided in the form of Nuget packages as part of the [Motion Applied Nuget Repository](https://github.com/mat-docs/packages).

See the [API Documentation](https://atlas.mclarenapplied.com/secu4/open_streaming_architecture/stream_api/) for more details.