# Stream.Api.Stream.Reader
## Introduction
This project is an example on how to consume data from the Stream API to any data format. This example supports recording to SQL Race.

## Getting Started
To use this example, please follow this [Getting Started Guide](https://atlas.mclarenapplied.com/secu4/open_streaming_architecture/bridge_service/#setup). Once everything is setup, you can use the `Config.json` to modify the configuration to suit your environment.

This program will initialize the Stream API Client and wait for a live session to be published in the Kafka using the Stream API. Once it has been found, it will connect to the session and start reading the stream coming from the Kafka via Stream API and writes the resulting data to SQL Race. Once the session is finished, The program will wait for the next session. It can handle recording multiple sessions at once.

## Config.json
The following options are available:
| Key | Default Value | Description |
| ----- | ----- | ----- |
| ipAddress | "localhost:13579" | The IP Address of the Stream API server. |
| dataSource | "Default" | The data source name which is used to identify the data source the session comes from. This should match to what is set in the Stream API server. If you are using the bridge service, this is set as the ADS name. |
| sqlRaceConnectionString | "" | The connection string used by SQL Race to record to a SQL Race Server, SSN2, or SQLite. |