using Google.Protobuf.Collections;
using MA.Streaming.API;
using MA.Streaming.OpenData;
using MA.Streaming.Proto.Client.Remote;
using MESL.SqlRace.Domain;

namespace Stream.Api.Stream.Reader
{
    internal class Session : ISession
    {
        // 10^9 * 3600 * 24 = 86400000000000
        private const long NumberOfNanosecondsInDay = 86400000000000;
        private readonly IClientSession clientSession;
        private readonly DataFormatManagerService.DataFormatManagerServiceClient dataFormatManagerServiceClient;
        private readonly string dataSource;
        private readonly PacketReaderService.PacketReaderServiceClient packetReaderServiceClient;
        private readonly AtlasSessionWriter sessionWriter;
        private readonly string streamApiSessionKey;
        private DateTime lastUpdated;
        public Session(IClientSession clientSession, string streamApiSessionKey,
            AtlasSessionWriter sessionWriter, string dataSource)
        {
            this.clientSession = clientSession;
            this.streamApiSessionKey = streamApiSessionKey;
            packetReaderServiceClient = RemoteStreamingApiClient.GetPacketReaderClient();
            dataFormatManagerServiceClient = RemoteStreamingApiClient.GetDataFormatManagerClient();
            this.sessionWriter = sessionWriter;
            this.dataSource = dataSource;
            this.lastUpdated = DateTime.Now;
        }

        public void GetEssentialPacket(CancellationToken cancellationToken, Connection connectionDetails)
        {
            var essentialsPacketStream = packetReaderServiceClient.ReadEssentials(new ReadEssentialsRequest()
                { Connection = connectionDetails }).ResponseStream;
            var task = Task.Run(async () =>
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        while (await essentialsPacketStream.MoveNext(cancellationToken))
                        {
                            var messages = essentialsPacketStream.Current.Response;

                            foreach (var message in messages)
                            { 
                                Console.WriteLine($"ESSENTIALS : New Packet {message.Packet.Type}");
                                HandleNewPacket(message.Packet);
                            };
                        }
                        return;
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unable to get essentials packet due to {ex.Message}.");
                }
            }, cancellationToken);
        }

        public void UpdateSessionInfo(GetSessionInfoResponse sessionInfo)
        {
            this.sessionWriter.UpdateSessionInfo(clientSession, sessionInfo);
        }

        public void ReadPackets(CancellationToken cancellationToken, Connection connectionDetails)
        {
            var packetStream = packetReaderServiceClient.ReadPackets(new ReadPacketsRequest()
            { Connection = connectionDetails });

            _ = Task.Run(async () =>
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        while (await packetStream.ResponseStream.MoveNext(cancellationToken))
                        {
                            var packetResponse = packetStream.ResponseStream.Current;
                            foreach (var response in packetResponse.Response)
                            {
                                //Console.WriteLine($"READ DATA: Packet received {response.Packet.Type}");
                                HandleNewPacket(response.Packet);
                            }
                        }
                    }
                    
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to read packet stream.");
                }
            }, cancellationToken);
        }

        public void EndSession()
        {
            // Wait for writing to session to end.
            do
            {

            }while(DateTime.Now - lastUpdated < TimeSpan.FromSeconds(10));
            sessionWriter.CloseSession(clientSession);
        }
        //private readonly object readDataLock = new object();
        public Task HandleNewPacket(Packet packet)
        {
            //Console.WriteLine("Received a new packet");
            if (packet.SessionKey != streamApiSessionKey)
            {
                Console.WriteLine("Session Key does not match. Ignoring the packet.");
                return Task.CompletedTask;
            }

            var packetType = packet.Type + "Packet";
            var content = packet.Content;
            try
            {
                switch (packetType)
                {
                    case "ConfigurationPacket":
                        {
                            ConfigurationPacket packetConfig = ConfigurationPacket.Parser.ParseFrom(content);
                            sessionWriter.AddConfiguration(clientSession, packetConfig);
                            break;
                        }
                    case "PeriodicDataPacket":
                        {
                            PeriodicDataPacket periodicDataPacket = PeriodicDataPacket.Parser.ParseFrom(content); 
                            HandlePeriodicPacket(periodicDataPacket);
                            //handler.Handle(periodicDataPacket);
                            break;
                        }
                    case "RowDataPacket":
                        {
                            RowDataPacket rowDataPacket = RowDataPacket.Parser.ParseFrom(content);
                            HandleRowData(rowDataPacket);
                            //handler.Handle(rowDataPacket);
                            break;
                        }
                    case "MarkerPacket":
                        {
                            MarkerPacket markerPacket = MarkerPacket.Parser.ParseFrom(content);
                            HandleMarkerPacket(markerPacket);
                            //handler.Handle(markerPacket);
                            break;
                        }
                    case "MetadataPacket":
                        {
                            MetadataPacket metadataPacket = MetadataPacket.Parser.ParseFrom(content);
                            HandleMetadataPacket(metadataPacket);
                            break;
                        }
                    case "EventPacket":
                        {
                            EventPacket eventPacket = EventPacket.Parser.ParseFrom(content);
                            HandleEventPacket(eventPacket);
                            //handler.Handle(eventPacket);
                            break;
                        }
                    default:
                        {
                            Console.WriteLine($"Unable to parse packet {packetType}.");
                            return Task.CompletedTask;
                        }
                }
                this.lastUpdated = DateTime.Now;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to handle packet {packetType} due to {ex.Message}");
                return Task.CompletedTask;
            }
            return Task.CompletedTask;
        }

        private void HandlePeriodicPacket(PeriodicDataPacket packet)
        {
            RepeatedField<string> parameterList;
            if (packet.DataFormat.HasDataFormatIdentifier)
                parameterList = dataFormatManagerServiceClient.GetParametersList(new GetParametersListRequest()
                        { DataFormatIdentifier = packet.DataFormat.DataFormatIdentifier, DataSource = dataSource })
                    .Parameters;
            else
                parameterList = packet.DataFormat.ParameterIdentifiers.ParameterIdentifiers;

            var newParameters = parameterList.Where(x => !sessionWriter.channelIdParameterDictionary.ContainsKey(x)).ToList();
            if (newParameters.Any())
            {
                sessionWriter.AddBasicParameterConfiguration(clientSession, newParameters);
            }

            var data = new List<double>();
            var status = new List<DataStatus>();
            var timestamps = new List<long>();
            for (int i = 0; i < parameterList.Count; i++)
            {
                var parameterIdentifier = parameterList[i];
                var column = packet.Columns[i];
                switch (column.ListCase)
                {
                    case SampleColumn.ListOneofCase.DoubleSamples:
                    {
                        for (var j = 0; j < column.DoubleSamples.Samples.Count; j++)
                        {
                            var dataSample = column.DoubleSamples.Samples[j];
                            data.Add(dataSample.Value);
                            status.Add(dataSample.Status);

                            timestamps.Add((long)(packet.StartTime + packet.Interval * (uint)j) %
                                           NumberOfNanosecondsInDay);
                        }

                        break;
                    }
                    case SampleColumn.ListOneofCase.Int32Samples:
                    {
                        for (var j = 0; j < column.Int32Samples.Samples.Count; j++)
                        {
                            var dataSample = column.Int32Samples.Samples[j];
                            data.Add(dataSample.Value);
                            status.Add(dataSample.Status);
                            timestamps.Add((long)(packet.StartTime + packet.Interval * (uint)j) %
                                           NumberOfNanosecondsInDay);
                        }

                        break;
                    }
                    case SampleColumn.ListOneofCase.BoolSamples:
                    {
                        for (var j = 0; j < column.BoolSamples.Samples.Count; j++)
                        {
                            var dataSample = column.BoolSamples.Samples[j];
                            data.Add(dataSample.Value ? 1.0 : 0.0);
                            status.Add(dataSample.Status);
                            timestamps.Add((long)(packet.StartTime + packet.Interval * (uint)j) %
                                           NumberOfNanosecondsInDay);
                        }

                        break;
                    }
                    case SampleColumn.ListOneofCase.StringSamples:
                    {
                        Console.WriteLine($"Unable to decode String Parameter {parameterIdentifier}");
                        continue;
                    }
                    case SampleColumn.ListOneofCase.None:
                    default:
                    {
                        Console.WriteLine($"No data found for parameter {parameterIdentifier}.");
                        continue;
                    }

                }

                if (!sessionWriter.TryAddData(clientSession, parameterIdentifier, data, timestamps))
                    Console.WriteLine($"Failed to write data to parameter {parameterIdentifier}.");
            }
        }

        private void HandleRowData(RowDataPacket packet)
        {
            RepeatedField<string> parameterList;
            if (packet.DataFormat.HasDataFormatIdentifier)
                parameterList = dataFormatManagerServiceClient.GetParametersList(new GetParametersListRequest()
                        { DataFormatIdentifier = packet.DataFormat.DataFormatIdentifier, DataSource = dataSource })
                    .Parameters;
            else
                parameterList = packet.DataFormat.ParameterIdentifiers.ParameterIdentifiers;

            var newParameters = parameterList.Where(x => !sessionWriter.channelIdParameterDictionary.ContainsKey(x)).ToList();
            if (newParameters.Any())
            {
                sessionWriter.AddBasicParameterConfiguration(clientSession, newParameters);
            }

            var parameterDictionary = new Dictionary<string, List<double>>();
            foreach (var parameter in parameterList)
            {
                parameterDictionary[parameter] = new List<double>();
            }
            var timestamps = new List<long>();
            for (var i = 0; i < packet.Rows.Count; i++)
            {
                var row = packet.Rows[i];
                timestamps.Add((long)packet.Timestamps[i] % NumberOfNanosecondsInDay);
                
                switch (row.ListCase)
                {
                    case SampleRow.ListOneofCase.DoubleSamples:
                    {
                        for (var j = 0; j < row.DoubleSamples.Samples.Count; j++)
                        {
                            
                            parameterDictionary[parameterList[j]].Add(row.DoubleSamples.Samples[j].Value);
                        }
                            
                        break;
                    }
                    case SampleRow.ListOneofCase.Int32Samples:
                    {
                        for (var j = 0; j < row.Int32Samples.Samples.Count; j++)
                        {
                            parameterDictionary[parameterList[j]].Add(row.Int32Samples.Samples[j].Value);
                        }
                            
                        break;
                    }
                    case SampleRow.ListOneofCase.BoolSamples:
                    {
                        for (var j = 0; j < row.BoolSamples.Samples.Count; j++)
                        {
                            parameterDictionary[parameterList[j]].Add(row.BoolSamples.Samples[j].Value ? 1.0 : 0.0);
                        }
                            
                        break;
                    }
                    case SampleRow.ListOneofCase.StringSamples:
                    {
                        Console.WriteLine("Unable to add String data for this row.");
                        continue;
                    }
                    case SampleRow.ListOneofCase.None:
                    default:
                    {
                        Console.WriteLine("No rows found for this packet.");
                        continue;
                    }
                }
            }

            foreach (var parameter in parameterDictionary)
                if (!sessionWriter.TryAddData(clientSession, parameter.Key, parameter.Value, timestamps))
                    Console.WriteLine($"Failed to write data to parameter {parameter.Key}");
        }

        private void HandleMarkerPacket(MarkerPacket packet)
        {
            var timestamp = (long)packet.Timestamp % NumberOfNanosecondsInDay;
            if (packet.Type == "Lap Trigger")
                sessionWriter.AddLap(clientSession, timestamp, (short)packet.Value, packet.Label, true);
            else
                sessionWriter.AddMarker(clientSession, timestamp, packet.Label);
        }

        private void HandleMetadataPacket(MetadataPacket packet)
        {
            Console.WriteLine("Metadata packets are unsupported.");
        }

        private void HandleEventPacket(EventPacket packet)
        {
            string eventIdentifier;
            if (packet.DataFormat.HasDataFormatIdentifier)
                eventIdentifier = dataFormatManagerServiceClient.GetEvent(new GetEventRequest()
                    { DataFormatIdentifier = packet.DataFormat.DataFormatIdentifier, DataSource = dataSource }).Event;
            else
                eventIdentifier = packet.DataFormat.EventIdentifier;

            if (!sessionWriter.eventDefCache.ContainsKey(eventIdentifier))
            {
                this.sessionWriter.AddBasicEventConfiguration(clientSession, eventIdentifier);
            }

            var timestamp = (long)packet.Timestamp % NumberOfNanosecondsInDay;
            var values = new List<double>();
            values.AddRange(packet.RawValues);
            sessionWriter.AddEvent(clientSession, eventIdentifier, timestamp, values);
        }
    }
}