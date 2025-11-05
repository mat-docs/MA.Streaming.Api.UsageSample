# C# Implementation

## Overview

The C# implementation provides a comprehensive WinForms sample application demonstrating all aspects of the Stream API, including session management, data format handling, packet reading/writing, and Docker-based deployment.

## Setup

### Prerequisites

- .NET 8.0 SDK or later
- Visual Studio 2022 (or JetBrains Rider)
- Docker Desktop (for Stream API services)
- ATLAS 10 (for SQLRace integration)

### Docker Setup

Before using the Stream API, you need to set up the required services using Docker. The Stream API requires Kafka and optionally a key generator service.

#### 1. Create Docker Compose Configuration

Create a `docker-compose.yml` file with the following content:

```yaml
name: kafka-compose

services:
  kafka:
    image: apache/kafka:latest
    hostname: kafka 
    container_name: kafka-broker-1
    networks:
      kafka_net_interal:
        ipv4_address: 172.22.0.7
    ports:
      - "9094:9094"
    environment:
      CLUSTER_ID: 'dev-kafka-cluster' 
      KAFKA_NODE_ID: 1
      KAFKA_PROCESS_ROLES: 'broker,controller'
      KAFKA_CONTROLLER_QUORUM_VOTERS: '1@kafka:9093' 
      KAFKA_LISTENERS: 'PLAINTEXT://:9092,CONTROLLER://:9093,PLAINTEXT_HOST://:9094'
      KAFKA_ADVERTISED_LISTENERS: 'PLAINTEXT://kafka:9092,PLAINTEXT_HOST://localhost:9094'
      KAFKA_LISTENER_SECURITY_PROTOCOL_MAP: 'CONTROLLER:PLAINTEXT,PLAINTEXT:PLAINTEXT,PLAINTEXT_HOST:PLAINTEXT'
      KAFKA_INTER_BROKER_LISTENER_NAME: 'PLAINTEXT'
      KAFKA_CONTROLLER_LISTENER_NAMES: 'CONTROLLER'
      KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 1
      KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR: 1
      KAFKA_TRANSACTION_STATE_LOG_MIN_ISR: 1
      KAFKA_GROUP_INITIAL_REBALANCE_DELAY_MS: 0
      KAFKA_LOG_DIRS: '/tmp/kraft-kafka-logs' 

  kafka-ui:
    image: provectuslabs/kafka-ui:latest 
    container_name: kafka-ui-1
    networks:
      kafka_net_interal:
        ipv4_address: 172.22.0.8
    ports:
      - "8080:8080" 
    environment:
      KAFKA_CLUSTERS_0_NAME: 'dev-kafka-cluster'
      KAFKA_CLUSTERS_0_BOOTSTRAPSERVERS: 'kafka:9092'
      DYNAMIC_CONFIG_ENABLED: 'true' 
    depends_on:
      - kafka 

  stream-api-server:
    image: atlasplatformdocker/streaming-proto-server-host-dev:1.3.6.33
    container_name: stream-api-server
    networks:
      kafka_net_interal:
        ipv4_address: 172.22.0.9
    ports:
      - 13579:13579
      - 10010:10010
    depends_on:
      - kafka
    environment:
      AUTO_START: true
    volumes:
      - ./configs:/app/Configs

  key-generator:
    image: atlasplatformdocker/keygenerator-proto-server-dev:1.3.6.12
    container_name: key-generator-server
    networks:
      kafka_net_interal:
        ipv4_address: 172.22.0.10
    ports:
      - 15379:15379
      - 10011:10010
    depends_on:
      - kafka
    environment:
      AUTO_START: true
    volumes:
      - ./key-configs:/app/Configs

networks:
  kafka_net_interal:
    driver: bridge
    ipam:
      config:
        - subnet: 172.22.0.0/16
          gateway: 172.22.0.1
```

#### 2. Create Configuration Directory

Create a `configs` directory and add a Stream API configuration file:

**configs/AppConfig.json:**
```json
{
  "StreamCreationStrategy": 1,
  "BrokerUrl": "kafka:9092",
  "PartitionMappings": [
    {
      "Stream": "Stream1",
      "Partition": 1
    },
    {
      "Stream": "Stream2", 
      "Partition": 2
    }
  ],
  "StreamApiPort": 13579,
  "EnableLogging": true,
  "EnableMetrics": true,
  "UseRemoteKeyGenerator": true,
  "RemoteKeyGeneratorServiceAddress": "key-generator:15379",
  "BatchingResponses": true
}
```

Optionally, create `key-configs` directory for key generator configuration (can be empty for default settings).

#### 3. Start the Services

```bash
docker-compose up -d
```

This will start:
- **Kafka Broker** (port 9094 for external connections)
- **Kafka UI** (port 8080 for monitoring)
- **Stream API Server** (port 13579)
- **Key Generator Service** (port 15379)

#### 4. Verify Services

Check that all services are running:

```bash
docker-compose ps
```

Expected output:
```
NAME                   COMMAND                  SERVICE             STATUS              PORTS
kafka-broker-1         "/opt/kafka/bin/kafk…"   kafka               running             0.0.0.0:9094->9094/tcp
kafka-ui-1            "java -jar kafka-ui-…"   kafka-ui            running             0.0.0.0:8080->8080/tcp
key-generator-server   "dotnet MA.Streaming…"   key-generator       running             0.0.0.0:15379->15379/tcp, 0.0.0.0:10011->10010/tcp
stream-api-server      "dotnet MA.Streaming…"   stream-api-server   running             0.0.0.0:10010->10010/tcp, 0.0.0.0:13579->13579/tcp
```

You can access the Kafka UI at http://localhost:8080 to monitor topics and messages.

#### 5. Test Connection

Test the Stream API connection with a simple C# program:

```csharp
using MA.Streaming.Proto.Client.Remote;
using MA.Streaming.API;

try
{
    // Initialize remote client
    RemoteStreamingApiClient.Initialise("localhost:13579");
    
    // Get session management client
    var sessionClient = RemoteStreamingApiClient.GetSessionManagementClient();
    
    // Test connection with a simple call
    var response = sessionClient.GetCurrentSessions(new GetCurrentSessionsRequest 
    { 
        DataSource = "test" 
    });
    
    Console.WriteLine($"Stream API connection successful! Success: {response.Success}");
}
catch (Exception ex)
{
    Console.WriteLine($"Connection failed: {ex.Message}");
}
```

#### 6. Stopping Services

To stop all services:

```bash
docker-compose down
```

To stop and remove all data:

```bash
docker-compose down -v
```

### NuGet Packages

The sample application uses the following key packages:

```xml
<PackageReference Include="MA.Streaming.Proto.Client.Remote" Version="x.x.x.x" />
<PackageReference Include="MA.Streaming.Proto.Client.Local" Version="x.x.x.x" />
<PackageReference Include="MA.Streaming.Core" Version="x.x.x.x" />
<PackageReference Include="MESL.SQLRace.API" Version="x.x.x.x" />
```

### Building the Project

```bash
cd C#/SampleApplication
dotnet restore
dotnet build
```

## Core Concepts

### Client Initialization

The Stream API supports two initialization modes:

#### Remote Client (gRPC)

Connect to a remote Stream API server:

```csharp
RemoteStreamingApiClient.Initialise("localhost:13579");

var sessionClient = RemoteStreamingApiClient.GetSessionManagementClient();
var packetWriterClient = RemoteStreamingApiClient.GetPacketWriterClient();
var packetReaderClient = RemoteStreamingApiClient.GetPacketReaderClient();
var dataFormatClient = RemoteStreamingApiClient.GetDataFormatManagerClient();
var connectionManagerClient = RemoteStreamingApiClient.GetConnectionManagerClient();
```

#### Local Client (In-Process)

Run the Stream API in-process for better performance:

```csharp
var configuration = new StreamingApiConfiguration(
    streamCreationStrategy: StreamCreationStrategy.PartitionBased,
    brokerUrl: "localhost:9094",
    partitionMappings: new List<PartitionMapping>
    {
        new PartitionMapping("Stream1", 1),
        new PartitionMapping("Stream2", 2)
    },
    streamApiPort: 13579,
    useRemoteKeyGenerator: true,
    remoteKeyGeneratorServiceAddress: "localhost:15379",
    batchingResponses: true
);

StreamingApiClient.Initialise(
    configuration,
    new CancellationTokenSourceProvider(),
    new KafkaBrokerAvailabilityChecker(),
    new LoggingDirectoryProvider("/logs")
);

var sessionClient = StreamingApiClient.GetSessionManagementClient();
var packetWriterClient = StreamingApiClient.GetPacketWriterClient();
var packetReaderClient = StreamingApiClient.GetPacketReaderClient();
var dataFormatClient = StreamingApiClient.GetDataFormatManagerClient();
var connectionManagerClient = StreamingApiClient.GetConnectionManagerClient();
```

## Usage Examples

### 1. Session Management

#### Creating a Session

```csharp
using MA.Streaming.API;
using Google.Protobuf.WellKnownTypes;

var request = new CreateSessionRequest
{
    DataSource = "SampleDataSource",
    Type = "Race",
    Version = 1,
    Identifier = "My Test Session",
    UtcOffset = Duration.FromTimeSpan(TimeSpan.FromHours(-5))
};

// Add session details
request.Details.Add("Driver", "Driver Name");
request.Details.Add("Track", "Track Name");
request.Details.Add("Weather", "Sunny");

// Associate with existing sessions
request.AssociateSessionKey.Add("parent-session-key");

var response = sessionManagementClient.CreateSession(request);

if (response.Success)
{
    string sessionKey = response.SessionKey;
    var newSessionPacket = response.NewSession;
    Console.WriteLine($"Session created with key: {sessionKey}");
}
```

#### Ending a Session

```csharp
var request = new EndSessionRequest
{
    DataSource = "SampleDataSource",
    SessionKey = sessionKey
};

var response = sessionManagementClient.EndSession(request);

if (response.Success)
{
    var endSessionPacket = response.EndSession;
    Console.WriteLine("Session ended successfully");
}
```

#### Getting Current Sessions

```csharp
var request = new GetCurrentSessionsRequest { DataSource = "SampleDataSource" };
var response = sessionManagementClient.GetCurrentSessions(request);

if (response.Success)
{
    foreach (var key in response.SessionKeys)
    {
        Console.WriteLine($"Active session: {key}");
    }
}
```

#### Querying Session Information

```csharp
var request = new GetSessionInfoRequest { SessionKey = sessionKey };
var response = sessionManagementClient.GetSessionInfo(request);

if (response.Success)
{
    Console.WriteLine($"DataSource: {response.DataSource}");
    Console.WriteLine($"Identifier: {response.Identifier}");
    Console.WriteLine($"Type: {response.Type}");
    Console.WriteLine($"Version: {response.Version}");
    Console.WriteLine($"IsComplete: {response.IsComplete}");
    Console.WriteLine($"Streams: {string.Join(", ", response.Streams)}");
    Console.WriteLine($"Main Offset: {response.MainOffset}");
    Console.WriteLine($"Essentials Offset: {response.EssentialsOffset}");
    
    foreach (var detail in response.Details)
    {
        Console.WriteLine($"Detail {detail.Key}: {detail.Value}");
    }
    
    foreach (var associateKey in response.AssociateSessionKeys)
    {
        Console.WriteLine($"Associated session: {associateKey}");
    }
}
```

#### Updating Session Identifier

```csharp
var request = new UpdateSessionIdentifierRequest
{
    SessionKey = sessionKey,
    Identifier = $"Updated Session {DateTime.Now}"
};

var response = sessionManagementClient.UpdateSessionIdentifier(request);

if (response.Success)
{
    Console.WriteLine("Session identifier updated successfully");
}
```

#### Adding Associated Sessions

```csharp
var request = new AddAssociateSessionRequest
{
    SessionKey = parentSessionKey,
    AssociateSessionKey = childSessionKey
};

var response = sessionManagementClient.AddAssociateSession(request);

if (response.Success)
{
    Console.WriteLine("Associated session added");
}
```

#### Updating Session Details

```csharp
var request = new UpdateSessionDetailsRequest
{
    SessionKey = sessionKey
};
request.Details.Add("Temperature", "25°C");
request.Details.Add("Humidity", "60%");
request.Details.Add("Tire_Compound", "Medium");

var response = sessionManagementClient.UpdateSessionDetails(request);

if (response.Success)
{
    Console.WriteLine("Session details updated successfully");
}
```

### 2. Session Notifications

#### Subscribe to New Session Notifications

```csharp
var request = new GetSessionStartNotificationRequest 
{ 
    DataSource = "SampleDataSource" 
};

var stream = sessionManagementClient.GetSessionStartNotification(request);

var cts = new CancellationTokenSource();
while (await stream.ResponseStream.MoveNext(cts.Token))
{
    var notification = stream.ResponseStream.Current;

    Console.WriteLine($"New session started:");
    Console.WriteLine($"  DataSource: {notification.DataSource}");
    Console.WriteLine($"  SessionKey: {notification.SessionKey}");
    
    // Handle new session
    HandleNewSession(notification.SessionKey);
}
```

#### Subscribe to Session Stop Notifications

```csharp
var request = new GetSessionStopNotificationRequest 
{ 
    DataSource = "SampleDataSource" 
};

var stream = sessionManagementClient.GetSessionStopNotification(request);

var cts = new CancellationTokenSource();
while (await stream.ResponseStream.MoveNext(cts.Token))
{
    var notification = stream.ResponseStream.Current;

    Console.WriteLine($"Session stopped:");
    Console.WriteLine($"  DataSource: {notification.DataSource}");
    Console.WriteLine($"  SessionKey: {notification.SessionKey}");
    
    // Handle session stop
    HandleSessionStop(notification.SessionKey);
}
```

### 3. Connection Management

#### Creating a Connection

```csharp
var connectionDetails = new ConnectionDetails
{
    DataSource = "SampleDataSource",
    SessionKey = sessionKey,
    MainOffset = 0, // Start from beginning
    EssentialsOffset = 0,
    ExcludeMainStream = false
};

// Specify specific streams and their offsets
connectionDetails.Streams.AddRange(new[] { "Stream1", "Stream2" });
connectionDetails.StreamOffsets.AddRange(new long[] { 0, -1 }); // Stream1 from start, Stream2 from latest

var request = new NewConnectionRequest { Details = connectionDetails };
var response = connectionManagerClient.NewConnection(request);

var connection = response.Connection;
Console.WriteLine($"Connection created with ID: {connection.Id}");
```

#### Getting Connection Details

```csharp
var request = new GetConnectionRequest { Connection = connection };
var response = connectionManagerClient.GetConnection(request);

var details = response.Details;
Console.WriteLine($"Connection reading from: {details.DataSource}");
Console.WriteLine($"Session: {details.SessionKey}");
Console.WriteLine($"Streams: {string.Join(", ", details.Streams)}");
Console.WriteLine($"Main offset: {details.MainOffset}");
Console.WriteLine($"Essentials offset: {details.EssentialsOffset}");
```

#### Closing a Connection

```csharp
var request = new CloseConnectionRequest { Connection = connection };
var response = connectionManagerClient.CloseConnection(request);

if (response.Success)
{
    Console.WriteLine("Connection closed successfully");
}
```

### 4. Data Format Management

#### Getting Parameter Data Format

```csharp
var request = new GetParameterDataFormatIdRequest
{
    DataSource = "SampleDataSource"
};
request.Parameters.AddRange(new[] { "Sin:MyApp", "Cos:MyApp", "Speed:Car" });

var response = dataFormatClient.GetParameterDataFormatId(request);
ulong dataFormatId = response.DataFormatIdentifier;

Console.WriteLine($"Parameter format ID: {dataFormatId}");
```

#### Getting Event Data Format

```csharp
var request = new GetEventDataFormatIdRequest
{
    DataSource = "SampleDataSource",
    Event = "LapComplete:Timing"
};

var response = dataFormatClient.GetEventDataFormatId(request);
ulong eventFormatId = response.DataFormatIdentifier;

Console.WriteLine($"Event format ID: {eventFormatId}");
```

#### Retrieving Parameters List

```csharp
var request = new GetParametersListRequest
{
    DataSource = "SampleDataSource",
    DataFormatIdentifier = dataFormatId
};

var response = dataFormatClient.GetParametersList(request);

foreach (var param in response.Parameters)
{
    Console.WriteLine($"Parameter: {param}");
}
```

#### Retrieving Event

```csharp
var request = new GetEventRequest
{
    DataSource = "SampleDataSource",
    DataFormatIdentifier = eventFormatId
};

var response = dataFormatClient.GetEvent(request);
string eventIdentifier = response.Event;

Console.WriteLine($"Event: {eventIdentifier}");
```

### 5. Writing Packets

#### Writing a Data Packet

```csharp
using MA.Streaming.OpenData;
using Google.Protobuf;

// Create a sample row data packet
var rowData = new RowDataPacket();
rowData.DataFormat = new SampleDataFormat
{
    ParameterIdentifiers = new ParameterList()
};
rowData.DataFormat.ParameterIdentifiers.ParameterIdentifiers.AddRange(
    new[] { "Sin:MyApp", "Cos:MyApp" }
);

// Add timestamps and data
rowData.Timestamps.AddRange(new ulong[] { 1000000, 2000000, 3000000 });

for (int i = 0; i < 3; i++)
{
    var row = new SampleRow
    {
        DoubleSamples = new DoubleSampleList()
    };
    
    row.DoubleSamples.Samples.Add(new DoubleSample 
    { 
        Value = Math.Sin(i), 
        Status = DataStatus.Valid 
    });
    
    row.DoubleSamples.Samples.Add(new DoubleSample 
    { 
        Value = Math.Cos(i), 
        Status = DataStatus.Valid 
    });
    
    rowData.Rows.Add(row);
}

// Create packet wrapper
var packet = new Packet
{
    Type = "RowData",
    SessionKey = sessionKey,
    IsEssential = false,
    Content = ByteString.CopyFrom(rowData.ToByteArray())
};

// Create packet details
var packetDetails = new DataPacketDetails
{
    Message = packet,
    DataSource = "SampleDataSource",
    Stream = "Stream1",
    SessionKey = sessionKey
};

// Write packet
var writeRequest = new WriteDataPacketRequest { Detail = packetDetails };
var writeResponse = packetWriterClient.WriteDataPacket(writeRequest);

Console.WriteLine("Data packet written successfully");
```

#### Writing Info Packets

```csharp
// Create system status packet
var statusMessage = new SystemStatusMessage
{
    Service = "DataAcquisition",
    DataSource = "SampleDataSource",
    Type = SystemStatusType.Status
};

var infoPacket = new Packet
{
    Type = "SystemStatusMessage",
    SessionKey = sessionKey,
    IsEssential = true,
    Content = ByteString.CopyFrom(statusMessage.ToByteArray())
};

// Write info packet
var infoRequest = new WriteInfoPacketRequest
{
    Message = infoPacket,
    Type = InfoType.SystemStatus
};

var infoResponse = packetWriterClient.WriteInfoPacket(infoRequest);
Console.WriteLine("Info packet written successfully");
```

#### Streaming Data Packets (Batch Writing)

For high-throughput scenarios, use client-streaming to send multiple packets efficiently:

```csharp
async Task WriteDataStreamBatched()
{
    var batchStream = packetWriterClient.WriteDataPackets();
    
    try
    {
        int batchSize = 100;
        var batch = new List<DataPacketDetails>();
        int totalSent = 0;
        
        Console.WriteLine("Starting to send packets via stream...");
        
        for (int i = 0; i < 1000; i++)
        {
            // Create row data packet
            var rowData = new RowDataPacket();
            // ... populate rowData ...
            
            var packet = new Packet
            {
                Type = "RowData",
                SessionKey = sessionKey,
                IsEssential = false,
                Content = ByteString.CopyFrom(rowData.ToByteArray())
            };
            
            var packetDetails = new DataPacketDetails
            {
                Message = packet,
                DataSource = "SampleDataSource",
                Stream = "Stream1",
                SessionKey = sessionKey
            };
            
            batch.Add(packetDetails);
            
            // When batch is full, send it
            if (batch.Count >= batchSize)
            {
                var request = new WriteDataPacketsRequest();
                request.Details.AddRange(batch);
                
                await batchStream.RequestStream.WriteAsync(request);
                
                totalSent += batch.Count;
                Console.WriteLine($"Sent batch {(totalSent / batchSize)} with {batch.Count} packets...");
                batch.Clear();
            }
        }
        
        // Send remaining packets
        if (batch.Count > 0)
        {
            var request = new WriteDataPacketsRequest();
            request.Details.AddRange(batch);
            
            await batchStream.RequestStream.WriteAsync(request);
            totalSent += batch.Count;
            Console.WriteLine($"Sent final batch with {batch.Count} packets...");
        }
        
        Console.WriteLine($"Total packets sent: {totalSent}. Closing stream...");
    }
    finally
    {
        await batchStream.RequestStream.CompleteAsync();
        var response = await batchStream;
        Console.WriteLine($"Stream closed. Server response received.");
    }
}
```

### 6. Reading Packets

#### Reading All Packets

```csharp
var readRequest = new ReadPacketsRequest { Connection = connection };
var readStream = packetReaderClient.ReadPackets(readRequest);

var cts = new CancellationTokenSource();
while (await readStream.ResponseStream.MoveNext(cts.Token))
{
    var response = readStream.ResponseStream.Current;
    
    foreach (var packetResponse in response.Response)
    {
        var packet = packetResponse.Packet;
        var stream = packetResponse.Stream;
        var submitTime = packetResponse.SubmitTime;
        
        Console.WriteLine($"Received packet:");
        Console.WriteLine($"  Type: {packet.Type}");
        Console.WriteLine($"  Stream: {stream}");
        Console.WriteLine($"  Session: {packet.SessionKey}");
        Console.WriteLine($"  Essential: {packet.IsEssential}");
        Console.WriteLine($"  Submit time: {submitTime}");
        
        // Process packet based on type
        ProcessPacket(packet);
    }
}
```

#### Reading Essential Packets Only

```csharp
var essentialsRequest = new ReadEssentialsRequest { Connection = connection };
var essentialsStream = packetReaderClient.ReadEssentials(essentialsRequest);

var cts = new CancellationTokenSource();
while (await essentialsStream.ResponseStream.MoveNext(cts.Token))
{
    var response = essentialsStream.ResponseStream.Current;
    
    foreach (var packetResponse in response.Response)
    {
        Console.WriteLine($"Essential packet: {packetResponse.Packet.Type}");
        ProcessEssentialPacket(packetResponse.Packet);
    }
}
```

#### Reading Filtered Data Packets

```csharp
// Create data packet filter
var dataRequest = new DataPacketRequest
{
    Connection = connection,
    IncludeMarkers = true
};

dataRequest.IncludeParameters.AddRange(new[] { "Sin:.*", "Cos:.*", "Speed:.*" });
dataRequest.ExcludeParameters.Add("Debug:.*");
dataRequest.IncludeEvents.AddRange(new[] { "Lap.*", "Sector.*" });
dataRequest.ExcludeEvents.Add("Heartbeat:.*");

var filteredRequest = new ReadDataPacketsRequest { Request = dataRequest };
var filteredStream = packetReaderClient.ReadDataPackets(filteredRequest);

var cts = new CancellationTokenSource();
while (await filteredStream.ResponseStream.MoveNext(cts.Token))
{
    var response = filteredStream.ResponseStream.Current;
    
    foreach (var packetResponse in response.Response)
    {
        // Only receives packets matching the filter
        Console.WriteLine($"Filtered packet: {packetResponse.Packet.Type}");
        ProcessFilteredPacket(packetResponse.Packet);
    }
}
```

### 7. Working with Open Data Packets

#### Processing NewSessionPacket

```csharp
void ProcessNewSessionPacket(byte[] packetData)
{
    var newSession = NewSessionPacket.Parser.ParseFrom(packetData);
    
    Console.WriteLine($"New session from: {newSession.DataSource}");
    Console.WriteLine($"UTC Offset: {newSession.UtcOffset}");
    
    if (newSession.SessionInfo != null)
    {
        var sessionInfo = newSession.SessionInfo;
        Console.WriteLine($"Session ID: {sessionInfo.Identifier}");
        Console.WriteLine($"Type: {sessionInfo.Type}");
        Console.WriteLine($"Version: {sessionInfo.Version}");
        
        foreach (var detail in sessionInfo.Details)
        {
            Console.WriteLine($"Detail {detail.Key}: {detail.Value}");
        }
    }
}
```

#### Processing ConfigurationPacket

```csharp
void ProcessConfigurationPacket(byte[] packetData)
{
    var config = ConfigurationPacket.Parser.ParseFrom(packetData);
    
    Console.WriteLine($"Configuration ID: {config.ConfigId}");
    
    foreach (var paramDef in config.ParameterDefinitions)
    {
        Console.WriteLine($"Parameter: {paramDef.Name}");
        Console.WriteLine($"  Identifier: {paramDef.Identifier}");
        Console.WriteLine($"  Units: {paramDef.Units}");
        Console.WriteLine($"  Type: {paramDef.DataType}");
        Console.WriteLine($"  Range: {paramDef.MinValue} - {paramDef.MaxValue}");
        Console.WriteLine($"  Groups: {string.Join(", ", paramDef.Groups)}");
    }
    
    foreach (var eventDef in config.EventDefinitions)
    {
        Console.WriteLine($"Event: {eventDef.Name}");
        Console.WriteLine($"  Identifier: {eventDef.Identifier}");
        Console.WriteLine($"  Priority: {eventDef.Priority}");
        Console.WriteLine($"  Application: {eventDef.ApplicationName}");
    }
    
    foreach (var groupDef in config.GroupDefinitions)
    {
        Console.WriteLine($"Group: {groupDef.Name}");
        Console.WriteLine($"  Identifier: {groupDef.Identifier}");
        Console.WriteLine($"  Application: {groupDef.ApplicationName}");
    }
}
```

#### Processing RowDataPacket

```csharp
void ProcessRowDataPacket(byte[] packetData)
{
    var rowData = RowDataPacket.Parser.ParseFrom(packetData);
    
    // Get format information
    string[] parameterNames = null;
    if (rowData.DataFormat.FormatCase == SampleDataFormat.FormatOneofCase.DataFormatIdentifier)
    {
        var formatId = rowData.DataFormat.DataFormatIdentifier;
        // Look up parameter list using format_id via DataFormatManagerService
        parameterNames = GetParameterNamesFromFormatId(formatId);
    }
    else if (rowData.DataFormat.FormatCase == SampleDataFormat.FormatOneofCase.ParameterIdentifiers)
    {
        parameterNames = rowData.DataFormat.ParameterIdentifiers.ParameterIdentifiers.ToArray();
    }
    
    Console.WriteLine($"Parameters: {string.Join(", ", parameterNames ?? new string[0])}");
    
    // Process data
    for (int i = 0; i < rowData.Timestamps.Count; i++)
    {
        Console.WriteLine($"Row {i}: timestamp={rowData.Timestamps[i]}");
        
        if (i < rowData.Rows.Count)
        {
            var row = rowData.Rows[i];
            
            // Process row data based on sample type
            if (row.ListCase == SampleRow.ListOneofCase.DoubleSamples)
            {
                for (int j = 0; j < row.DoubleSamples.Samples.Count; j++)
                {
                    var sample = row.DoubleSamples.Samples[j];
                    var paramName = parameterNames != null && j < parameterNames.Length ? parameterNames[j] : $"Param{j}";
                    Console.WriteLine($"  {paramName}: {sample.Value} (status: {sample.Status})");
                }
            }
            else if (row.ListCase == SampleRow.ListOneofCase.Int32Samples)
            {
                for (int j = 0; j < row.Int32Samples.Samples.Count; j++)
                {
                    var sample = row.Int32Samples.Samples[j];
                    var paramName = parameterNames != null && j < parameterNames.Length ? parameterNames[j] : $"Param{j}";
                    Console.WriteLine($"  {paramName}: {sample.Value} (status: {sample.Status})");
                }
            }
            // Handle other sample types as needed...
        }
    }
}

string[] GetParameterNamesFromFormatId(ulong formatId)
{
    var request = new GetParametersListRequest
    {
        DataSource = "SampleDataSource",
        DataFormatIdentifier = formatId
    };
    
    var response = dataFormatClient.GetParametersList(request);
    return response.Parameters.ToArray();
}
```

#### Processing EventPacket

```csharp
void ProcessEventPacket(byte[] packetData)
{
    var eventPacket = EventPacket.Parser.ParseFrom(packetData);
    
    string eventIdentifier = null;
    if (eventPacket.DataFormat.FormatCase == EventDataFormat.FormatOneofCase.DataFormatIdentifier)
    {
        var formatId = eventPacket.DataFormat.DataFormatIdentifier;
        // Look up event identifier using format_id
        eventIdentifier = GetEventFromFormatId(formatId);
    }
    else if (eventPacket.DataFormat.FormatCase == EventDataFormat.FormatOneofCase.EventIdentifier)
    {
        eventIdentifier = eventPacket.DataFormat.EventIdentifier;
    }
    
    Console.WriteLine($"Event: {eventIdentifier}");
    Console.WriteLine($"Timestamp: {eventPacket.Timestamp}");
    Console.WriteLine($"Values: [{string.Join(", ", eventPacket.RawValues)}]");
}

string GetEventFromFormatId(ulong formatId)
{
    var request = new GetEventRequest
    {
        DataSource = "SampleDataSource",
        DataFormatIdentifier = formatId
    };
    
    var response = dataFormatClient.GetEvent(request);
    return response.Event;
}
```

#### Processing MarkerPacket

```csharp
void ProcessMarkerPacket(byte[] packetData)
{
    var marker = MarkerPacket.Parser.ParseFrom(packetData);
    
    Console.WriteLine($"Marker: {marker.Label}");
    Console.WriteLine($"Start time: {marker.Timestamp}");
    if (marker.EndTime != 0)
    {
        Console.WriteLine($"End time: {marker.EndTime}");
    }
    Console.WriteLine($"Type: {marker.Type}");
    Console.WriteLine($"Description: {marker.Description}");
    Console.WriteLine($"Source: {marker.Source}");
    if (marker.Value != 0)
    {
        Console.WriteLine($"Value: {marker.Value}");
    }
}
```

### 8. Key Generator Service

#### Generating Unique Keys

```csharp
using MA.Streaming.KeyGenerator;

// Generate string key
var stringRequest = new GenerateUniqueKeyRequest
{
    Type = KeyType.string
};

var stringResponse = keyGeneratorClient.GenerateUniqueKey(stringRequest);
if (stringResponse.KeyCase == GenerateUniqueKeyResponse.KeyOneofCase.StringKey)
{
    string uniqueKey = stringResponse.StringKey;
    Console.WriteLine($"Generated string key: {uniqueKey}");
}

// Generate ulong key
var ulongRequest = new GenerateUniqueKeyRequest
{
    Type = KeyType.Ulong
};

var ulongResponse = keyGeneratorClient.GenerateUniqueKey(ulongRequest);
if (ulongResponse.KeyCase == GenerateUniqueKeyResponse.KeyOneofCase.UlongKey)
{
    ulong uniqueId = ulongResponse.UlongKey;
    Console.WriteLine($"Generated ulong key: {uniqueId}");
}
```

## Best Practices

1. **Use Connections**: Always use ConnectionManager for reading to maintain proper offset tracking
2. **Connection Management**: Close connections when done to free resources
3. **Error Handling**: Wrap all gRPC calls in try-catch blocks and check Success flags
4. **Async/Await**: Use async methods for better scalability
5. **Resource Cleanup**: Dispose of clients and streams properly
6. **Configuration**: Externalize configuration to JSON files
7. **Monitoring**: Use Prometheus metrics for production deployments
8. **Docker**: Use containerized deployment for consistent environments

## Troubleshooting

### Common Issues

**Issue**: Session creation returns Success=false
```csharp
// Solution: Check response and handle failure case
var response = sessionManagementClient.CreateSession(request);
if (!response.Success)
{
    Console.WriteLine("Session creation failed - check broker connectivity and data source");
    return;
}
```

**Issue**: Connection not receiving packets
```csharp
// Solution: Verify connection details and session existence
try
{
    var response = connectionManagerClient.NewConnection(request);
    var connection = response.Connection;
    
    // Verify connection details
    var getConnResponse = connectionManagerClient.GetConnection(
        new GetConnectionRequest { Connection = connection }
    );
    Console.WriteLine($"Connection details: {getConnResponse.Details}");
}
catch (RpcException ex)
{
    Console.WriteLine($"Connection error: {ex.Status.StatusCode} - {ex.Status.Detail}");
}
```

**Issue**: Data format ID not found
```csharp
// Solution: Ensure configuration packets are written before requesting format IDs
try
{
    var response = dataFormatClient.GetParameterDataFormatId(request);
    var formatId = response.DataFormatIdentifier;
    
    if (formatId == 0)
    {
        Console.WriteLine("Format ID not found - ensure configuration is published first");
    }
}
catch (RpcException ex)
{
    Console.WriteLine($"Format lookup error: {ex.Status.StatusCode} - {ex.Status.Detail}");
}
```
