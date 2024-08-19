# Stream.Api.Stream.Reader
## Introduction
This project is an example on how to consume data from the Stream API to any data format. This example supports 3 different formats: SQL Race, SQL Server DB, and CSVs.

## Getting Started
To use this example, please follow this [Getting Started Guide](). Once everything is setup, you can use the `Config.json` to modify the configuration to suit your environment.

This program will initialize the Stream API Client and wait for a live session to be published in the Kafka using the Stream API. Once it has been found, it will connect to the session and start reading the stream coming from the Kafka via Stream API and writes the resulting data to one of the supported output formats. Once the session is finished, the program will need to be restarted before another recording can be done.

## Config.json
The following options are available:
| Key | Default Value | Description |
| ----- | ----- | ----- |
| rootPath | "C:\\Temp\\" | The root path where all CSVs will be written to when output format is set to CSV. |
| ipAddress | "localhost:13579" | The IP Address of the Stream API server. |
| dataSource | "Default" | The data source name which is used to identify the data source the session comes from. This should match to what is set in the Stream API server. If you are using the bridge service, this is set as the ADS name. |
| dbPath | "C:\\McLaren Applied\\StreamAPIDemo.ssndb" | The path of the ssndb file for recording to SQL Race |
| outputFormat | 1 | Sets the output format to record to. `1` = SQL Race, `2` = CSV, `3` = SQL Server DB |
| sqlDbConnectionString | "" | The connection string used to connect to a SQL Server DB. |