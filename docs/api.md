# API Reference

## Core Components

### StreamApi

Main interface for interacting with the streaming service.

#### Methods

- `ConnectAsync()`: Establishes connection to the streaming service
- `SubscribeAsync(string stream)`: Subscribes to a specific data stream
- `PublishAsync(string stream, byte[] data)`: Publishes data to a stream

## Session Management Service

The Session Management Service handles the lifecycle of streaming sessions, including creation, updates, monitoring, and notifications.

### CreateSession

Creates a new streaming session for a data source.

**Method Signature (C#):**
```csharp
CreateSessionResponse CreateSession(CreateSessionRequest request)
```

**Method Signature (Python):**
```python
create_session_response = session_stub.CreateSession(
    api_pb2.CreateSessionRequest(data_source=DATA_SOURCE)
)
```

**Parameters:**

- `data_source` (string): Unique identifier for the data source. This distinguishes different data producers (e.g., "SampleDataSource", "Telemetry_Car1")
- `type` (string, optional): Session type identifier (e.g., "Session", "Race", "Testing"). Defaults to "Session"
- `version` (uint32, optional): Version number for the session format. Defaults to 1
- `utc_offset` (Duration, optional): Difference between UTC time and local standard time in the timezone where data is recorded
- `identifier` (string, optional): Human-readable session identifier
- `associate_session_key` (repeated string, optional): List of session keys to associate with this session
- `details` (map<string, string>, optional): Session details as key-value pairs

**Returns:**

- `session_key` (string): Unique identifier for the newly created session
- `new_session` (NewSessionPacket): The new session packet that was created
- `success` (bool): Whether the session creation succeeded

**Example (C#):**
```csharp
var request = new CreateSessionRequest
{
    DataSource = "SampleDataSource",
    Type = "Race",
    Version = 1,
    Identifier = "My Test Session",
    UtcOffset = Duration.FromTimeSpan(TimeSpan.FromHours(-5))
};
request.Details.Add("Driver", "Lewis Hamilton");
request.Details.Add("Track", "Silverstone");

var response = sessionManagementClient.CreateSession(request);
string sessionKey = response.SessionKey;
bool success = response.Success;
```

**Example (Python):**
```python
from google.protobuf.duration_pb2 import Duration

utc_offset = Duration()
utc_offset.FromTimedelta(timedelta(hours=-5))

create_session_response = session_stub.CreateSession(
    api_pb2.CreateSessionRequest(
        data_source="SampleDataSource",
        type="Race",
        version=1,
        identifier="My Test Session",
        utc_offset=utc_offset,
        associate_session_key=["parent-session-key"],
        details={"Driver": "Lewis Hamilton", "Track": "Silverstone"}
    )
)
session_key = create_session_response.session_key
```

---

### EndSession

Ends an existing streaming session.

**Method Signature (C#):**
```csharp
EndSessionResponse EndSession(EndSessionRequest request)
```

**Parameters:**

- `data_source` (string): The data source identifier
- `session_key` (string): The unique session key to end

**Returns:**

- `end_session` (EndOfSessionPacket): The end of session packet that was created
- `success` (bool): Whether the session was successfully ended

**Example (C#):**
```csharp
var request = new EndSessionRequest
{
    DataSource = "SampleDataSource",
    SessionKey = sessionKey
};
var response = sessionManagementClient.EndSession(request);
```

---

### GetCurrentSessions

Retrieves all active session keys for a given data source.

**Method Signature (C#):**
```csharp
GetCurrentSessionsResponse GetCurrentSessions(GetCurrentSessionsRequest request)
```

**Parameters:**

- `data_source` (string): The data source identifier to query

**Returns:**

- `session_keys` (repeated string): List of all active session keys for the data source
- `success` (bool): Whether the request succeeded

**Example (C#):**
```csharp
var request = new GetCurrentSessionsRequest { DataSource = "SampleDataSource" };
var response = sessionManagementClient.GetCurrentSessions(request);
foreach (var key in response.SessionKeys)
{
    Console.WriteLine($"Active session: {key}");
}
```

---

### GetSessionInfo

Retrieves detailed information about a specific session.

**Method Signature (C#):**
```csharp
GetSessionInfoResponse GetSessionInfo(GetSessionInfoRequest request)
```

**Parameters:**

- `session_key` (string): The unique session key to query

**Returns:**

- `data_source` (string): The data source identifier
- `identifier` (string): The session's human-readable identifier
- `type` (string): Session type
- `version` (uint32): Session version number
- `associate_session_keys` (repeated string): List of associated session keys
- `is_complete` (bool): Whether the session has been completed
- `streams` (repeated string): Available streams for this session
- `topic_partition_offsets` (map<string, int64>): Current offsets for each topic/partition
- `main_offset` (int64): Offset of the main data source topic
- `essentials_offset` (int64): Offset of the essentials topic
- `details` (map<string, string>): Session details as key-value pairs
- `utc_offset` (Duration): UTC offset for the session
- `success` (bool): Whether the request succeeded

**Example (C#):**
```csharp
var request = new GetSessionInfoRequest { SessionKey = sessionKey };
var response = sessionManagementClient.GetSessionInfo(request);
Console.WriteLine($"DataSource: {response.DataSource}");
Console.WriteLine($"Identifier: {response.Identifier}");
Console.WriteLine($"Type: {response.Type}");
Console.WriteLine($"Version: {response.Version}");
Console.WriteLine($"IsComplete: {response.IsComplete}");
foreach (var detail in response.Details)
{
    Console.WriteLine($"Detail {detail.Key}: {detail.Value}");
}
```

---

### UpdateSessionIdentifier

Updates the human-readable identifier for an existing session.

**Method Signature (C#):**
```csharp
UpdateSessionIdentifierResponse UpdateSessionIdentifier(UpdateSessionIdentifierRequest request)
```

**Parameters:**

- `session_key` (string): The unique session key
- `identifier` (string): New human-readable name for the session

**Returns:**

- `success` (bool): True if the identifier was successfully updated

---

### AddAssociateSession

Associates one session with another, creating a parent-child relationship.

**Method Signature (C#):**
```csharp
AddAssociateSessionResponse AddAssociateSession(AddAssociateSessionRequest request)
```

**Parameters:**

- `session_key` (string): The parent session key
- `associate_session_key` (string): The child session key to associate

**Returns:**

- `success` (bool): True if the association was successfully created

---

### UpdateSessionDetails

Updates the details (metadata) for an existing session.

**Method Signature (C#):**
```csharp
UpdateSessionDetailsResponse UpdateSessionDetails(UpdateSessionDetailsRequest request)
```

**Parameters:**

- `session_key` (string): The unique session key
- `details` (map<string, string>): Session details to update as key-value pairs

**Returns:**

- `success` (bool): True if the details were successfully updated

**Example (C#):**
```csharp
var request = new UpdateSessionDetailsRequest
{
    SessionKey = sessionKey
};
request.Details.Add("Weather", "Sunny");
request.Details.Add("Temperature", "25°C");

var response = sessionManagementClient.UpdateSessionDetails(request);
```

---

### Session Notifications

Subscribe to real-time notifications for session events.

#### GetSessionStartNotification

Receives notifications when new sessions are created.

**Parameters:**

- `data_source` (string): The data source to monitor for new sessions

**Returns:** Stream of notifications containing:
- `session_key` (string): The key of the newly created session
- `data_source` (string): The data source of the new session

#### GetSessionStopNotification

Receives notifications when sessions are ended.

**Parameters:**

- `data_source` (string): The data source to monitor for ended sessions

**Returns:** Stream of notifications containing:
- `session_key` (string): The key of the ended session
- `data_source` (string): The data source of the ended session

---

## Data Format Management Service

The Data Format Management Service handles parameter and event format definitions, allowing clients to understand and interpret streamed data efficiently.

### GetParameterDataFormatId

Retrieves the data format identifier for a specific list of parameters.

**Method Signature (C#):**
```csharp
GetParameterDataFormatIdResponse GetParameterDataFormatId(GetParameterDataFormatIdRequest request)
```

**Parameters:**

- `data_source` (string): The data source identifier
- `parameters` (repeated string): Ordered list of parameter identifiers. The same parameters in different order result in different format IDs

**Returns:**

- `data_format_identifier` (uint64): Unique identifier for this parameter list's data format

**Example (C#):**
```csharp
var request = new GetParameterDataFormatIdRequest
{
    DataSource = "SampleDataSource"
};
request.Parameters.AddRange(new[] { "Sin:MyApp", "Cos:MyApp" });

var response = dataFormatClient.GetParameterDataFormatId(request);
ulong formatId = response.DataFormatIdentifier;
```

---

### GetEventDataFormatId

Retrieves the data format identifier for a specific event.

**Method Signature (C#):**
```csharp
GetEventDataFormatIdResponse GetEventDataFormatId(GetEventDataFormatIdRequest request)
```

**Parameters:**

- `data_source` (string): The data source identifier
- `event` (string): The event identifier (e.g., "LapComplete:Timing")

**Returns:**

- `data_format_identifier` (uint64): Unique identifier for the event's data format

**Example (C#):**
```csharp
var request = new GetEventDataFormatIdRequest
{
    DataSource = "SampleDataSource",
    Event = "LapComplete:Timing"
};
var response = dataFormatClient.GetEventDataFormatId(request);
ulong formatId = response.DataFormatIdentifier;
```

---

### GetParametersList

Retrieves parameter list using a data format identifier.

**Method Signature (C#):**
```csharp
GetParametersListResponse GetParametersList(GetParametersListRequest request)
```

**Parameters:**

- `data_source` (string): The data source identifier
- `data_format_identifier` (uint64): The data format identifier

**Returns:**

- `parameters` (repeated string): List of parameter identifiers associated with this format

**Example (C#):**
```csharp
var request = new GetParametersListRequest
{
    DataSource = "SampleDataSource",
    DataFormatIdentifier = formatId
};
var response = dataFormatClient.GetParametersList(request);
foreach (var param in response.Parameters)
{
    Console.WriteLine($"Parameter: {param}");
}
```

---

### GetEvent

Retrieves event identifier using a data format identifier.

**Method Signature (C#):**
```csharp
GetEventResponse GetEvent(GetEventRequest request)
```

**Parameters:**

- `data_source` (string): The data source identifier
- `data_format_identifier` (uint64): The data format identifier

**Returns:**

- `event` (string): Event identifier associated with this format

**Example (C#):**
```csharp
var request = new GetEventRequest
{
    DataSource = "SampleDataSource",
    DataFormatIdentifier = formatId
};
var response = dataFormatClient.GetEvent(request);
string eventIdentifier = response.Event;
```

---

## Connection Manager Service

The Connection Manager Service handles creating and managing connections for reading data streams. Connections maintain current offset positions for efficient streaming.

### NewConnection

Creates a new connection for reading data.

**Method Signature (C#):**
```csharp
NewConnectionResponse NewConnection(NewConnectionRequest request)
```

**Parameters:**

- `details` (ConnectionDetails): Connection configuration containing:
  - `data_source` (string): Data source to read from
  - `session_key` (string, optional): Session key of the session to read
  - `streams` (repeated string, optional): Specific streams to read (omit for all streams)
  - `stream_offsets` (repeated int64, optional): Starting offset for each stream (-1 = latest, 0 = earliest)
  - `main_offset` (int64, optional): Starting offset for main data source topic
  - `essentials_offset` (int64, optional): Starting offset for essentials topic
  - `exclude_main_stream` (bool, optional): Whether to exclude the main stream from reading

**Returns:**

- `connection` (Connection): Connection identifier for use in subsequent operations

**Example (C#):**
```csharp
var connectionDetails = new ConnectionDetails
{
    DataSource = "SampleDataSource",
    SessionKey = sessionKey,
    MainOffset = 0, // Start from beginning
    EssentialsOffset = 0
};
connectionDetails.Streams.AddRange(new[] { "Stream1", "Stream2" });
connectionDetails.StreamOffsets.AddRange(new long[] { 0, 0 });

var request = new NewConnectionRequest { Details = connectionDetails };
var response = connectionManagerClient.NewConnection(request);
var connection = response.Connection;
```

---

### GetConnection

Retrieves details of an existing connection.

**Method Signature (C#):**
```csharp
GetConnectionResponse GetConnection(GetConnectionRequest request)
```

**Parameters:**

- `connection` (Connection): The connection identifier

**Returns:**

- `details` (ConnectionDetails): Current connection configuration

---

### CloseConnection

Closes an existing connection and releases resources.

**Method Signature (C#):**
```csharp
CloseConnectionResponse CloseConnection(CloseConnectionRequest request)
```

**Parameters:**

- `connection` (Connection): The connection identifier to close

**Returns:**

- `success` (bool): Whether the connection was successfully closed

---

## Packet Writer Service

The Packet Writer Service handles publishing data packets and info packets to streams.

### WriteDataPacket

Writes a single data packet to a stream.

**Method Signature (C#):**
```csharp
WriteDataPacketResponse WriteDataPacket(WriteDataPacketRequest request)
```

**Parameters:**

- `detail` (DataPacketDetails): Packet details containing:
  - `message` (Packet): The packet to write (from open_data.proto)
  - `data_source` (string): Data source identifier
  - `stream` (string): Target stream name
  - `session_key` (string): Session key

**Returns:** Empty response (success indicated by lack of gRPC error)

**Example (C#):**
```csharp
var packet = new Packet
{
    Type = "RowDataPacket",
    SessionKey = sessionKey,
    IsEssential = false,
    Content = ByteString.CopyFrom(serializedData)
};

var details = new DataPacketDetails
{
    Message = packet,
    DataSource = "SampleDataSource",
    Stream = "Stream1",
    SessionKey = sessionKey
};

var request = new WriteDataPacketRequest { Detail = details };
var response = packetWriterClient.WriteDataPacket(request);
```

---

### WriteDataPackets

Continuously writes a stream of data packets.

**Method Signature (C#):**
```csharp
WriteDataPacketsResponse WriteDataPackets(IAsyncStreamWriter<WriteDataPacketsRequest> requestStream)
```

**Parameters:**

- `details` (repeated DataPacketDetails): Stream of packet details to write

**Returns:** Response after all packets are written

---

### WriteInfoPacket

Writes a single info packet.

**Method Signature (C#):**
```csharp
WriteInfoPacketResponse WriteInfoPacket(WriteInfoPacketRequest request)
```

**Parameters:**

- `message` (Packet): The info packet to write
- `type` (InfoType): Type of info packet:
  - `INFO_TYPE_SESSION_INFO`: Session information
  - `INFO_TYPE_SYSTEM_STATUS`: System status information

**Returns:** Empty response

---

### WriteInfoPackets

Continuously writes a stream of info packets.

---

## Packet Reader Service

The Packet Reader Service handles reading and subscribing to data streams using connections.

### ReadPackets

Continuously reads all packets from a connection.

**Method Signature (C#):**
```csharp
IAsyncEnumerable<ReadPacketsResponse> ReadPackets(ReadPacketsRequest request)
```

**Parameters:**

- `connection` (Connection): The connection to read from

**Returns:** Stream of packet responses, each containing:
- `response` (repeated PacketResponse): List of packets read, each with:
  - `packet` (Packet): The packet that was read
  - `stream` (string): The stream the packet was read from
  - `submit_time` (Timestamp): Time when packet was submitted to the broker

**Example (C#):**
```csharp
var request = new ReadPacketsRequest { Connection = connection };
var stream = packetReaderClient.ReadPackets(request);

await foreach (var response in stream)
{
    foreach (var packetResponse in response.Response)
    {
        Console.WriteLine($"Received packet from stream: {packetResponse.Stream}");
        Console.WriteLine($"Packet type: {packetResponse.Packet.Type}");
        Console.WriteLine($"Submit time: {packetResponse.SubmitTime}");
        
        // Process packet content
        ProcessPacket(packetResponse.Packet);
    }
}
```

---

### ReadEssentials

Continuously reads only essential packets from a connection.

**Method Signature (C#):**
```csharp
IAsyncEnumerable<ReadEssentialsResponse> ReadEssentials(ReadEssentialsRequest request)
```

**Parameters:**

- `connection` (Connection): The connection to read from

**Returns:** Stream of essential packet responses

---

### ReadDataPackets

Continuously reads filtered data packets containing specified parameters or events.

**Method Signature (C#):**
```csharp
IAsyncEnumerable<ReadDataPacketsResponse> ReadDataPackets(ReadDataPacketsRequest request)
```

**Parameters:**

- `request` (DataPacketRequest): Filter configuration containing:
  - `connection` (Connection): Connection to read from
  - `include_parameters` (repeated string): Parameter patterns to include (regex)
  - `exclude_parameters` (repeated string): Parameter patterns to exclude (regex)
  - `include_events` (repeated string): Event patterns to include (regex)
  - `exclude_events` (repeated string): Event patterns to exclude (regex)
  - `include_markers` (bool): Whether to include marker packets

**Returns:** Stream of filtered data packet responses

**Example (C#):**
```csharp
var dataRequest = new DataPacketRequest
{
    Connection = connection,
    IncludeMarkers = true
};
dataRequest.IncludeParameters.AddRange(new[] { "Sin:.*", "Cos:.*" });
dataRequest.ExcludeEvents.Add("Debug:.*");

var request = new ReadDataPacketsRequest { Request = dataRequest };
var stream = packetReaderClient.ReadDataPackets(request);

await foreach (var response in stream)
{
    // Only receives data packets matching the filter criteria
    foreach (var packetResponse in response.Response)
    {
        ProcessFilteredPacket(packetResponse.Packet);
    }
}
```

---

## Key Generator Service

The Key Generator Service provides unique key generation for sessions and other entities.

### GenerateUniqueKey

Generates a unique key of the specified type.

**Method Signature (C#):**
```csharp
GenerateUniqueKeyResponse GenerateUniqueKey(GenerateUniqueKeyRequest request)
```

**Parameters:**

- `type` (KeyType): The type of key to generate:
  - `KEY_TYPE_STRING`: String-based unique key
  - `KEY_TYPE_ULONG`: 64-bit unsigned integer key

**Returns:**

- `key` (oneof): The generated key, either:
  - `string_key` (string): Generated string key
  - `ulong_key` (uint64): Generated unsigned long key

**Example (C#):**
```csharp
var request = new GenerateUniqueKeyRequest
{
    Type = KeyType.KeyTypeString
};
var response = keyGeneratorClient.GenerateUniqueKey(request);

if (response.KeyCase == GenerateUniqueKeyResponse.KeyOneofCase.StringKey)
{
    string uniqueKey = response.StringKey;
    Console.WriteLine($"Generated key: {uniqueKey}");
}
```

---

## Open Data Packet Types

The Stream API uses various packet types defined in `open_data.proto` for different kinds of data.

### NewSessionPacket

Indicates a new session has started.

**Fields:**
- `data_source` (string): Data source name
- `topic_partition_offsets` (map<string, int64>): Current offsets for topics/partitions
- `utc_offset` (Duration): UTC offset for the session
- `session_info` (SessionInfoPacket, optional): Session information

### EndOfSessionPacket

Indicates a session has ended.

**Fields:**
- `data_source` (string): Data source name
- `topic_partition_offsets` (map<string, int64>): Final offsets for topics/partitions

### ConfigurationPacket

Defines parameters and events for a session.

**Fields:**
- `config_id` (string): Configuration identifier
- `parameter_definitions` (repeated ParameterDefinition): Parameter definitions
- `event_definitions` (repeated EventDefinition): Event definitions
- `group_definitions` (repeated GroupDefinition): Group definitions

### RowDataPacket

Contains timestamped telemetry data in row format.

**Fields:**
- `data_format` (SampleDataFormat): Data format specification
- `timestamps` (repeated fixed64): Timestamps for each row
- `rows` (repeated SampleRow): Data rows

### PeriodicDataPacket

Contains equally-spaced telemetry data.

**Fields:**
- `data_format` (SampleDataFormat): Data format specification
- `start_time` (fixed64): Timestamp of first sample
- `interval` (uint32): Time between samples
- `columns` (repeated SampleColumn): Data columns

### EventPacket

Contains event data.

**Fields:**
- `data_format` (EventDataFormat): Event format specification
- `timestamp` (fixed64): Event timestamp
- `raw_values` (repeated double): Event-specific data values

### MarkerPacket

Contains marker/annotation data.

**Fields:**
- `timestamp` (fixed64): Start time of marker
- `end_time` (fixed64, optional): End time of marker
- `label` (string): Text label
- `type` (string): Marker type
- `description` (string): Description
- `source` (string): Source of the marker
- `value` (int64): Marker value if applicable

---

## Error Handling

All API methods may throw gRPC exceptions in case of:

- Network failures
- Invalid parameters
- Session not found
- Permission issues
- Kafka broker unavailability

**Example Error Handling (C#):**
```csharp
try
{
    var response = sessionManagementClient.CreateSession(request);
    if (!response.Success)
    {
        Console.WriteLine("Session creation failed");
    }
}
catch (RpcException ex)
{
    Console.WriteLine($"RPC Error: {ex.Status.StatusCode} - {ex.Status.Detail}");
}
```

**Example Error Handling (Python):**
```python
try:
    response = session_stub.CreateSession(request)
    if not response.success:
        print("Session creation failed")
except grpc.RpcError as e:
    print(f"RPC Error: {e.code()} - {e.details()}")
```
