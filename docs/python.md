# Python Implementation

## Overview

The Python implementation provides a comprehensive interface to the Stream API, enabling real-time data streaming, session management, and SQLRace integration for data persistence.

## Setup

### Prerequisites
- Python 3.8 or later
- gRPC tools
- Protocol buffer compiler
- SQLRace API (for SQLRace integration)
- Docker Desktop (for Stream API services)

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
    image: atlasplatformdocker/streaming-proto-server-host:latest
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

Create a `configs` directory and add a basic Stream API configuration file:

```bash
mkdir configs
```

Create `configs/AppConfig.json`:

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
  "RemoteKeyGeneratorServiceAddress": "key-generator:15379"
}
```

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

You can access the Kafka UI at http://localhost:8080 to monitor topics and messages.

#### 5. Test Connection

Test the Stream API connection:

```python
import grpc
from ma.streaming.api.v1 import api_pb2, api_pb2_grpc

# Test connection to Stream API
try:
    channel = grpc.insecure_channel('localhost:13579')
    stub = api_pb2_grpc.SessionManagementServiceStub(channel)
    # Try a simple operation
    response = stub.GetCurrentSessions(
        api_pb2.GetCurrentSessionsRequest(data_source="test")
    )
    print("Stream API connection successful!")
except Exception as e:
    print(f"Connection failed: {e}")
```

### Installation

Install the required gRPC tools:

```bash
python -m pip install grpcio-tools
```

### Compiling Protocol Buffers

Python definitions can be generated from the proto files. The latest proto file can be found on [GitHub](https://github.com/Software-Products/MA.DataPlatforms.Protocol).

1. Clone the protocol repository:
   ```bash
   git clone https://github.com/Software-Products/MA.DataPlatforms.Protocol.git
   ```

2. Navigate to the cloned repository directory:
   ```bash
   cd MA.DataPlatforms.Protocol
   ```

3. Verify the directory structure exists:
   ```powershell
   # Check if the proto directories exist
   ls proto\ma\streaming\api\v1
   ls proto\ma\streaming\open_data\v1
   ```

4. Generate the Python gRPC code from the proto files:

   **On Windows (PowerShell):**
   ```powershell
   python -m grpc_tools.protoc -Iproto --python_out=. --grpc_python_out=. proto\ma\streaming\api\v1\api.proto proto\ma\streaming\open_data\v1\open_data.proto
   ```

   **On Linux/Mac:**
   ```bash
   python -m grpc_tools.protoc -Iproto --python_out=. --grpc_python_out=. proto/ma/streaming/api/v1/api.proto proto/ma/streaming/open_data/v1/open_data.proto
   ```

   **If there are multiple proto files to compile:**
   
   Windows PowerShell:
   ```powershell
   Get-ChildItem -Path proto\ma\streaming\api\v1\*.proto | ForEach-Object { python -m grpc_tools.protoc -Iproto --python_out=. --grpc_python_out=. $_.FullName }
   Get-ChildItem -Path proto\ma\streaming\open_data\v1\*.proto | ForEach-Object { python -m grpc_tools.protoc -Iproto --python_out=. --grpc_python_out=. $_.FullName }
   ```

   Linux/Mac:
   ```bash
   for proto_file in proto/ma/streaming/api/v1/*.proto proto/ma/streaming/open_data/v1/*.proto; do
     python -m grpc_tools.protoc -Iproto --python_out=. --grpc_python_out=. "$proto_file"
   done
   ```

5. Move the generated files to your project directory:

**On Windows (PowerShell):**
```powershell
# Create your project directory structure
New-Item -ItemType Directory -Force -Path my_stream_project\ma\streaming\api\v1
New-Item -ItemType Directory -Force -Path my_stream_project\ma\streaming\open_data\v1

# Copy the generated Python files
Copy-Item ma\streaming\api\v1\*_pb2.py my_stream_project\ma\streaming\api\v1\
Copy-Item ma\streaming\api\v1\*_pb2_grpc.py my_stream_project\ma\streaming\api\v1\
Copy-Item ma\streaming\open_data\v1\*_pb2.py my_stream_project\ma\streaming\open_data\v1\
Copy-Item ma\streaming\open_data\v1\*_pb2_grpc.py my_stream_project\ma\streaming\open_data\v1\

# Add __init__.py files to make them Python packages
New-Item -ItemType File -Path my_stream_project\ma\__init__.py
New-Item -ItemType File -Path my_stream_project\ma\streaming\__init__.py
New-Item -ItemType File -Path my_stream_project\ma\streaming\api\__init__.py
New-Item -ItemType File -Path my_stream_project\ma\streaming\api\v1\__init__.py
New-Item -ItemType File -Path my_stream_project\ma\streaming\open_data\__init__.py
New-Item -ItemType File -Path my_stream_project\ma\streaming\open_data\v1\__init__.py
```

**On Linux/Mac:**
```bash
# Create your project directory structure
mkdir -p my_stream_project/ma/streaming/api/v1
mkdir -p my_stream_project/ma/streaming/open_data/v1

# Copy the generated Python files
cp ma/streaming/api/v1/*_pb2.py my_stream_project/ma/streaming/api/v1/
cp ma/streaming/api/v1/*_pb2_grpc.py my_stream_project/ma/streaming/api/v1/
cp ma/streaming/open_data/v1/*_pb2.py my_stream_project/ma/streaming/open_data/v1/
cp ma/streaming/open_data/v1/*_pb2_grpc.py my_stream_project/ma/streaming/open_data/v1/

# Add __init__.py files to make them Python packages
touch my_stream_project/ma/__init__.py
touch my_stream_project/ma/streaming/__init__.py
touch my_stream_project/ma/streaming/api/__init__.py
touch my_stream_project/ma/streaming/api/v1/__init__.py
touch my_stream_project/ma/streaming/open_data/__init__.py
touch my_stream_project/ma/streaming/open_data/v1/__init__.py
```

This will generate the necessary Python files (`*_pb2.py` and `*_pb2_grpc.py`) in the appropriate directories.


## Core Concepts

### Stream API Client

You have two options for accessing the Stream API services:

#### Option 1: Using the StreamApi Helper Class (Recommended)

Create a `stream_api.py` file in your project to organize all services:

```python
"""All the Stream API services organised in a single class."""

import grpc
from ma.streaming.api.v1 import api_pb2_grpc


class StreamApi:
    """All the Stream API services organised in a single class."""

    def __init__(self, address="localhost:13579"):
        self.channel = grpc.insecure_channel(address)

        # Create the gRPC clients
        self.connection_manager_service_stub = (
            api_pb2_grpc.ConnectionManagerServiceStub(self.channel)
        )
        self.session_management_service_stub = (
            api_pb2_grpc.SessionManagementServiceStub(self.channel)
        )
        self.data_format_manager_service_stub = (
            api_pb2_grpc.DataFormatManagerServiceStub(self.channel)
        )
        self.packet_reader_service_stub = api_pb2_grpc.PacketReaderServiceStub(
            self.channel
        )
        self.packet_writer_service_stub = api_pb2_grpc.PacketWriterServiceStub(
            self.channel
        )
```

Then use it in your application:

```python
from stream_api import StreamApi

stream_api = StreamApi()
session_stub = stream_api.session_management_service_stub
data_format_stub = stream_api.data_format_manager_service_stub
packet_writer_stub = stream_api.packet_writer_service_stub
packet_reader_stub = stream_api.packet_reader_service_stub
connection_manager_stub = stream_api.connection_manager_service_stub
```

#### Option 2: Creating Stubs Directly

Alternatively, create the service stubs directly without a helper class:

```python
import grpc
from ma.streaming.api.v1 import api_pb2, api_pb2_grpc

# Create gRPC channel
channel = grpc.insecure_channel('localhost:13579')

# Create service stubs
session_stub = api_pb2_grpc.SessionManagementServiceStub(channel)
data_format_stub = api_pb2_grpc.DataFormatManagerServiceStub(channel)
packet_writer_stub = api_pb2_grpc.PacketWriterServiceStub(channel)
packet_reader_stub = api_pb2_grpc.PacketReaderServiceStub(channel)
connection_manager_stub = api_pb2_grpc.ConnectionManagerServiceStub(channel)
```

## Usage Examples

### 1. Session Management

#### Creating a Session

Create a new streaming session with comprehensive configuration:

```python
from ma.streaming.api.v1 import api_pb2
from google.protobuf.duration_pb2 import Duration
import datetime

DATA_SOURCE = "SampleDataSource"

# Create UTC offset (e.g., -5 hours for EST)
utc_offset = Duration()
utc_offset.FromTimedelta(datetime.timedelta(hours=-5))

# Create session with full configuration
create_session_response = session_stub.CreateSession(
    api_pb2.CreateSessionRequest(
        data_source=DATA_SOURCE,
        type="Race",
        version=1,
        utc_offset=utc_offset,
        identifier=f"Test Session {datetime.datetime.now()}",
        associate_session_key=["parent-session-key"],
        details={
            "Driver": "Driver Name",
            "Track": "Track Name",
            "Weather": "Sunny"
        }
    )
)

session_key = create_session_response.session_key
success = create_session_response.success
new_session_packet = create_session_response.new_session

print(f"Session created: {session_key}, Success: {success}")
```

#### Ending a Session

Properly end a session when complete:

```python
end_response = session_stub.EndSession(
    api_pb2.EndSessionRequest(
        data_source=DATA_SOURCE,
        session_key=session_key
    )
)

if end_response.success:
    print("Session ended successfully")
    end_session_packet = end_response.end_session
```

#### Getting All Sessions

Query all sessions for a data source:

```python
sessions_response = session_stub.GetCurrentSessions(
    api_pb2.GetCurrentSessionsRequest(
        data_source=DATA_SOURCE
    )
)

if sessions_response.success:
    for session_key in sessions_response.session_keys:
        print(f"Active session: {session_key}")
```

#### Retrieving Session Information

Get session details:

```python
import time

# Wait for session to be published to broker
time.sleep(1)

session_info_response = session_stub.GetSessionInfo(
    api_pb2.GetSessionInfoRequest(
        session_key=session_key,
    )
)

if session_info_response.success:
    print(f"DataSource: {session_info_response.data_source}")
    print(f"Identifier: {session_info_response.identifier}")
    print(f"Type: {session_info_response.type}")
    print(f"Version: {session_info_response.version}")
    print(f"Is Complete: {session_info_response.is_complete}")
    print(f"Streams: {list(session_info_response.streams)}")
    print(f"Main Offset: {session_info_response.main_offset}")
    print(f"Essentials Offset: {session_info_response.essentials_offset}")
    
    for key, value in session_info_response.details.items():
        print(f"Detail {key}: {value}")
```

#### Updating Session Details

Add or update session metadata:

```python
update_details_response = session_stub.UpdateSessionDetails(
    api_pb2.UpdateSessionDetailsRequest(
        session_key=session_key,
        details={
            "Temperature": "25°C",
            "Humidity": "60%",
            "Tire_Compound": "Medium"
        }
    )
)

if update_details_response.success:
    print("Session details updated")
```

#### Adding Associated Sessions

Link related sessions together:

```python
associate_response = session_stub.AddAssociateSession(
    api_pb2.AddAssociateSessionRequest(
        session_key=parent_session_key,
        associate_session_key=child_session_key
    )
)

if associate_response.success:
    print("Sessions associated successfully")
```

### 2. Session Notifications

#### Subscribe to New Session Notifications

Monitor for new sessions in real-time:

```python
def monitor_new_sessions(data_source):
    request = api_pb2.GetSessionStartNotificationRequest(
        data_source=data_source
    )
    
    for notification in session_stub.GetSessionStartNotification(request):
        print(f"New session started:")
        print(f"  DataSource: {notification.data_source}")
        print(f"  SessionKey: {notification.session_key}")
        
        # Handle new session
        handle_new_session(notification.session_key)

# Start monitoring in a separate thread
import threading
monitor_thread = threading.Thread(
    target=monitor_new_sessions, 
    args=("SampleDataSource",)
)
monitor_thread.daemon = True
monitor_thread.start()
```

#### Subscribe to Session End Notifications

Monitor for session completions:

```python
def monitor_session_ends(data_source):
    request = api_pb2.GetSessionStopNotificationRequest(
        data_source=data_source
    )
    
    for notification in session_stub.GetSessionStopNotification(request):
        print(f"Session ended:")
        print(f"  DataSource: {notification.data_source}")
        print(f"  SessionKey: {notification.session_key}")
        
        # Handle session end
        handle_session_end(notification.session_key)
```

### 3. Connection Management

#### Creating a Connection

Create a connection for reading data with specific configuration:

```python
# Create connection details
connection_details = api_pb2.ConnectionDetails(
    data_source=DATA_SOURCE,
    session_key=session_key,
    streams=["Stream1", "Stream2"],
    stream_offsets=[0, 0],  # Start from beginning
    main_offset=0,
    essentials_offset=0,
    exclude_main_stream=False
)

# Create connection
connection_response = connection_manager_stub.NewConnection(
    api_pb2.NewConnectionRequest(details=connection_details)
)

connection = connection_response.connection
print(f"Connection created with ID: {connection.id}")
```

#### Getting Connection Details

Retrieve current connection configuration:

```python
get_conn_response = connection_manager_stub.GetConnection(
    api_pb2.GetConnectionRequest(connection=connection)
)

details = get_conn_response.details
print(f"Connection reading from: {details.data_source}")
print(f"Session: {details.session_key}")
print(f"Streams: {list(details.streams)}")
```

#### Closing a Connection

Clean up connection resources:

```python
close_response = connection_manager_stub.CloseConnection(
    api_pb2.CloseConnectionRequest(connection=connection)
)

if close_response.success:
    print("Connection closed successfully")
```

### 4. Data Format Management

#### Getting Parameter Data Format

Get format ID for a specific parameter list:

```python
format_response = data_format_stub.GetParameterDataFormatId(
    api_pb2.GetParameterDataFormatIdRequest(
        data_source=DATA_SOURCE,
        parameters=["Sin:MyApp", "Cos:MyApp", "Speed:Car"]
    )
)

data_format_id = format_response.data_format_identifier
print(f"Parameter format ID: {data_format_id}")
```

#### Getting Event Data Format

Get format ID for a specific event:

```python
event_format_response = data_format_stub.GetEventDataFormatId(
    api_pb2.GetEventDataFormatIdRequest(
        data_source=DATA_SOURCE,
        event="LapComplete:Timing"
    )
)

event_format_id = event_format_response.data_format_identifier
print(f"Event format ID: {event_format_id}")
```

#### Retrieving Parameters List

Get parameter list from format ID:

```python
params_response = data_format_stub.GetParametersList(
    api_pb2.GetParametersListRequest(
        data_source=DATA_SOURCE,
        data_format_identifier=data_format_id
    )
)

for param in params_response.parameters:
    print(f"Parameter: {param}")
```

#### Retrieving Event

Get event identifier from format ID:

```python
event_response = data_format_stub.GetEvent(
    api_pb2.GetEventRequest(
        data_source=DATA_SOURCE,
        data_format_identifier=event_format_id
    )
)

print(f"Event: {event_response.event}")
```

### 5. Writing Data Packets

#### Writing a Data Packet

Write a single data packet to a stream:

```python
from ma.streaming.open_data.v1 import open_data_pb2

# Create a sample packet
packet = open_data_pb2.Packet(
    type="RowData",
    session_key=session_key,
    is_essential=False,
    content=serialized_data
)

# Create packet details
packet_details = api_pb2.DataPacketDetails(
    message=packet,
    data_source=DATA_SOURCE,
    stream="Stream1",
    session_key=session_key
)

# Write packet
write_response = packet_writer_stub.WriteDataPacket(
    api_pb2.WriteDataPacketRequest(detail=packet_details)
)

print("Data packet written successfully")
```

#### Writing Info Packets

Write system status or session info packets:

```python
# Create info packet
info_packet = open_data_pb2.Packet(
    type="SystemStatusMessage",
    session_key=session_key,
    is_essential=True,
    content=status_data
)

# Write info packet
info_response = packet_writer_stub.WriteInfoPacket(
    api_pb2.WriteInfoPacketRequest(
        message=info_packet,
        type=api_pb2.INFO_TYPE_SYSTEM_STATUS
    )
)

print("Info packet written successfully")
```

#### Streaming Data Packets

**Method 1: Batch multiple packets in each request (Recommended)**

This approach uses client-streaming to send multiple packets efficiently. The generator creates the stream, keeps it alive while sending batches, and closes it when exhausted:

```python
import os
import grpc

def write_data_stream_batched():
    """
    Properly use WriteDataPackets streaming API:
    1. Create a generator that yields batches
    2. Pass generator to WriteDataPackets - this creates the stream
    3. Generator exhaustion closes the stream
    4. Server then sends response after stream closes
    """
    
    def generate_and_send_batches():
        """Generator that keeps the stream alive while sending batches"""
        batch_size = 100
        batch = []
        total_sent = 0
        
        print("Starting to send packets via stream...")
        
        for i in range(1000):
            packet = open_data_pb2.Packet(
                type="RowData",
                session_key=session_key,
                is_essential=False,
                content=os.urandom(16),
            )
            
            packet_details = api_pb2.DataPacketDetails(
                message=packet,
                data_source=DATA_SOURCE,
                stream="Stream1",
                session_key=session_key,
            )
            
            batch.append(packet_details)
            
            # When batch is full, yield it (keeps stream alive)
            if len(batch) >= batch_size:
                print(f"Sending batch {(total_sent // batch_size) + 1} with {len(batch)} packets...")
                yield api_pb2.WriteDataPacketsRequest(details=batch)
                total_sent += len(batch)
                batch = []
        
        # Send remaining packets
        if batch:
            print(f"Sending final batch with {len(batch)} packets...")
            yield api_pb2.WriteDataPacketsRequest(details=batch)
            total_sent += len(batch)
        
        print(f"Total packets sent: {total_sent}. Stream will now close.")
        # When generator ends, stream closes automatically
    
    try:
        # Call WriteDataPackets with generator
        # The generator keeps stream alive, then closes when exhausted
        # Server processes in background and sends response after stream closes
        print("Creating stream and sending batches...")
        response = packet_writer_stub.WriteDataPackets(
            generate_and_send_batches(),
            timeout=10,  # Add timeout since server uses background processing
        )
        print(f"Stream closed. Server response received: {response}")
    except grpc.RpcError as e:
        if e.code() == grpc.StatusCode.DEADLINE_EXCEEDED:
            print("Timeout waiting for response, but packets were sent successfully")
            print("(Server uses background processing and may not send timely response)")
        else:
            print(f"gRPC Error: {e.code()} - {e.details()}")
    except Exception as e:
        print(f"Error: {type(e).__name__}: {e}")

# Call the function
write_data_stream_batched()
```

### 6. Reading Data Packets

#### Reading All Packets

Read all packets from a connection:

```python
read_request = api_pb2.ReadPacketsRequest(connection=connection)

for packet_response in packet_reader_stub.ReadPackets(read_request):
    for packet_resp in packet_response.response:
        packet = packet_resp.packet
        stream = packet_resp.stream
        submit_time = packet_resp.submit_time
        
        print(f"Received packet:")
        print(f"  Type: {packet.type}")
        print(f"  Stream: {stream}")
        print(f"  Session: {packet.session_key}")
        print(f"  Essential: {packet.is_essential}")
        print(f"  Submit time: {submit_time}")
        
        # Process packet based on type
        process_packet(packet)
```

#### Reading Essential Packets Only

Read only essential packets (configuration, session info, etc.):

```python
essentials_request = api_pb2.ReadEssentialsRequest(connection=connection)

for packet_response in packet_reader_stub.ReadEssentials(essentials_request):
    for packet_resp in packet_response.response:
        print(f"Essential packet: {packet_resp.packet.type}")
        process_essential_packet(packet_resp.packet)
```

#### Reading Filtered Data Packets

Read only data packets matching specific criteria:

```python
# Create data packet filter
data_request = api_pb2.DataPacketRequest(
    connection=connection,
    include_parameters=["Sin:.*", "Cos:.*", "Speed:.*"],
    exclude_parameters=["Debug:.*"],
    include_events=["Lap.*", "Sector.*"],
    exclude_events=["Heartbeat:.*"],
    include_markers=True
)

filtered_request = api_pb2.ReadDataPacketsRequest(request=data_request)

for packet_response in packet_reader_stub.ReadDataPackets(filtered_request):
    for packet_resp in packet_response.response:
        # Only receives packets matching the filter
        print(f"Filtered packet: {packet_resp.packet.type}")
        process_filtered_packet(packet_resp.packet)
```

### 7. Working with Open Data Packets

#### Processing NewSessionPacket

```python
def process_new_session_packet(packet_data):
    new_session = open_data_pb2.NewSessionPacket()
    new_session.ParseFromString(packet_data)
    
    print(f"New session from: {new_session.data_source}")
    print(f"UTC Offset: {new_session.utc_offset}")
    
    if new_session.HasField('session_info'):
        session_info = new_session.session_info
        print(f"Session ID: {session_info.identifier}")
        print(f"Type: {session_info.type}")
        print(f"Version: {session_info.version}")
```

#### Processing ConfigurationPacket

```python
def process_configuration_packet(packet_data):
    config = open_data_pb2.ConfigurationPacket()
    config.ParseFromString(packet_data)
    
    print(f"Configuration ID: {config.config_id}")
    
    for param_def in config.parameter_definitions:
        print(f"Parameter: {param_def.name}")
        print(f"  Identifier: {param_def.identifier}")
        print(f"  Units: {param_def.units}")
        print(f"  Type: {param_def.data_type}")
        print(f"  Range: {param_def.min_value} - {param_def.max_value}")
    
    for event_def in config.event_definitions:
        print(f"Event: {event_def.name}")
        print(f"  Identifier: {event_def.identifier}")
        print(f"  Priority: {event_def.priority}")
```

#### Processing RowDataPacket

```python
def process_row_data_packet(packet_data):
    row_data = open_data_pb2.RowDataPacket()
    row_data.ParseFromString(packet_data)
    
    # Get format information
    if row_data.data_format.HasField('data_format_identifier'):
        format_id = row_data.data_format.data_format_identifier
        # Look up parameter list using format_id
    elif row_data.data_format.HasField('parameter_identifiers'):
        params = row_data.data_format.parameter_identifiers.parameter_identifiers
        print(f"Parameters: {list(params)}")
    
    # Process data
    for i, timestamp in enumerate(row_data.timestamps):
        print(f"Row {i}: timestamp={timestamp}")
        
        if i < len(row_data.rows):
            row = row_data.rows[i]
            # Process row data based on sample type
            if row.HasField('double_samples'):
                for j, sample in enumerate(row.double_samples.samples):
                    print(f"  Value {j}: {sample.value} (status: {sample.status})")
```

### 8. SQLRace Integration

#### Creating SQLRace Session Writer

```python
from stream_reader_sqlrace.atlas_session_writer import AtlasSessionWriter

# Create session writer
writer = AtlasSessionWriter(
    data_source="localhost\\SQLRACE",
    database="SQLRACE01",
    session_identifier="Stream API Demo Session"
)

# Process configuration packet
def handle_configuration(config_packet):
    writer.add_configration(config_packet)
    print(f"Configuration added: {config_packet.config_id}")

# Process data packets
def handle_row_data(row_data_packet):
    for i, timestamp in enumerate(row_data_packet.timestamps):
        if i < len(row_data_packet.rows):
            row = row_data_packet.rows[i]
            # Extract parameter values and write to SQLRace
            # Implementation depends on data format
            pass

# Cleanup
writer.close_session()
```

### 9. Complete Example: Stream Reader with Real-time Processing

```python
import threading
import time
from collections import defaultdict

class StreamProcessor:
    def __init__(self, data_source):
        self.data_source = data_source
        self.connections = {}
        self.parameter_cache = {}
        self.running = False
        
    def start_monitoring(self):
        """Start monitoring for new sessions"""
        self.running = True
        
        # Monitor new sessions
        monitor_thread = threading.Thread(target=self._monitor_sessions)
        monitor_thread.daemon = True
        monitor_thread.start()
        
    def _monitor_sessions(self):
        """Monitor for new session notifications"""
        request = api_pb2.GetSessionStartNotificationRequest(
            data_source=self.data_source
        )
        
        for notification in session_stub.GetSessionStartNotification(request):
            if self.running:
                print(f"New session detected: {notification.session_key}")
                self._start_session_processing(notification.session_key)
    
    def _start_session_processing(self, session_key):
        """Start processing a specific session"""
        # Create connection for this session
        connection_details = api_pb2.ConnectionDetails(
            data_source=self.data_source,
            session_key=session_key,
            main_offset=0,
            essentials_offset=0
        )
        
        connection_response = connection_manager_stub.NewConnection(
            api_pb2.NewConnectionRequest(details=connection_details)
        )
        
        connection = connection_response.connection
        self.connections[session_key] = connection
        
        # Start reading packets in separate thread
        read_thread = threading.Thread(
            target=self._read_packets,
            args=(connection, session_key)
        )
        read_thread.daemon = True
        read_thread.start()
    
    def _read_packets(self, connection, session_key):
        """Read and process packets from a connection"""
        read_request = api_pb2.ReadPacketsRequest(connection=connection)
        
        try:
            for packet_response in packet_reader_stub.ReadPackets(read_request):
                for packet_resp in packet_response.response:
                    self._process_packet(packet_resp.packet, session_key)
        except Exception as e:
            print(f"Error reading packets: {e}")
        finally:
            # Clean up connection
            connection_manager_stub.CloseConnection(
                api_pb2.CloseConnectionRequest(connection=connection)
            )
    
    def _process_packet(self, packet, session_key):
        """Process individual packets based on type"""
        if packet.type == "NewSession":
            self._handle_new_session(packet.content, session_key)
        elif packet.type == "Configuration":
            self._handle_configuration(packet.content, session_key)
        elif packet.type == "RowData":
            self._handle_row_data(packet.content, session_key)
        elif packet.type == "Event":
            self._handle_event(packet.content, session_key)
        elif packet.type == "Marker":
            self._handle_marker(packet.content, session_key)
    
    def _handle_new_session(self, packet_data, session_key):
        new_session = open_data_pb2.NewSessionPacket()
        new_session.ParseFromString(packet_data)
        print(f"Session {session_key} started from {new_session.data_source}")
    
    def _handle_configuration(self, packet_data, session_key):
        config = open_data_pb2.ConfigurationPacket()
        config.ParseFromString(packet_data)
        
        # Cache parameter definitions
        for param_def in config.parameter_definitions:
            self.parameter_cache[param_def.identifier] = param_def
        
        print(f"Configuration loaded for session {session_key}")
    
    def _handle_row_data(self, packet_data, session_key):
        row_data = open_data_pb2.RowDataPacket()
        row_data.ParseFromString(packet_data)
        
        # Process telemetry data
        parameter_count = 0
        if row_data.data_format.HasField('parameter_identifiers'):
            parameter_count = len(row_data.data_format.parameter_identifiers.parameter_identifiers)
        
        print(f"Received {len(row_data.timestamps)} samples for {parameter_count} parameters")
    
    def _handle_event(self, packet_data, session_key):
        event = open_data_pb2.EventPacket()
        event.ParseFromString(packet_data)
        
        print(f"Event at {event.timestamp}: {len(event.raw_values)} values")
    
    def _handle_marker(self, packet_data, session_key):
        marker = open_data_pb2.MarkerPacket()
        marker.ParseFromString(packet_data)
        
        print(f"Marker: {marker.label} at {marker.timestamp}")
    
    def stop(self):
        """Stop monitoring and clean up"""
        self.running = False
        
        # Close all connections
        for connection in self.connections.values():
            try:
                connection_manager_stub.CloseConnection(
                    api_pb2.CloseConnectionRequest(connection=connection)
                )
            except:
                pass

# Usage
processor = StreamProcessor("SampleDataSource")
processor.start_monitoring()

# Let it run for a while
time.sleep(30)

processor.stop()
```

## Best Practices

1. **Connection Management**: Always close connections when done to free resources
2. **Error Handling**: Wrap all gRPC calls in try-except blocks
3. **Threading**: Use separate threads for long-running stream operations
4. **Caching**: Cache configuration data to avoid repeated lookups
5. **Filtering**: Use filtered reading for better performance when processing specific data
6. **Batching**: Use streaming writes for high-throughput scenarios
7. **Resource Cleanup**: Always clean up connections and close SQLRace sessions

## Troubleshooting

### Common Issues

**Issue**: Session not found after creation
```python
# Solution: Add delay for broker synchronization
time.sleep(1)
```

**Issue**: Connection fails to read packets
```python
# Solution: Verify connection details and session existence
try:
    response = connection_manager_stub.NewConnection(request)
except grpc.RpcError as e:
    print(f"Connection error: {e.code()} - {e.details()}")
```

**Issue**: Packet parsing errors
```python
# Solution: Check packet type before parsing
if packet.type == "RowData":
    row_data = open_data_pb2.RowDataPacket()
    try:
        row_data.ParseFromString(packet.content)
    except Exception as e:
        print(f"Failed to parse packet: {e}")
```
