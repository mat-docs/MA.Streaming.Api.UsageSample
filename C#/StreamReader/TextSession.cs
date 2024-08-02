using System.Globalization;
using CsvHelper;
using Google.Protobuf.Collections;
using MA.Streaming.API;
using MA.Streaming.OpenData;
using MA.Streaming.Proto.Client.Remote;

namespace Stream.Api.Stream.Reader
{
    internal class TextSession
    {
        private readonly PacketReaderService.PacketReaderServiceClient packetReaderServiceClient;
        private readonly DataFormatManagerService.DataFormatManagerServiceClient dataFormatManagerServiceClient;
        private string parameterCsv;
        private string eventCsv;
        private string markersCsv;
        private string rootFolderPath;
        public string sessionName;
        private DateTime lastUpdated;
        private readonly string streamApiSessionKey;
        private readonly string dataSource;
        private List<EventText> eventsRecords = new List<EventText>();
        private List<LapText> lapsRecords = new List<LapText>();
        private List<ParameterText> parametersPeriodicRecords = new List<ParameterText>();
        private List<ParameterText> parametersRowRecords = new List<ParameterText>();
        public TextSession(string rootFolderPath, string sessionName, string streamApiSessionKey, string dataSource)
        {
            this.parameterCsv = rootFolderPath + $"{sessionName}_parameters.csv";
            this.eventCsv = rootFolderPath + $"{sessionName}_events.csv";
            this.markersCsv = rootFolderPath + $"{sessionName}_markers.csv";
            this.rootFolderPath = rootFolderPath;
            this.packetReaderServiceClient = RemoteStreamingApiClient.GetPacketReaderClient();
            this.dataFormatManagerServiceClient = RemoteStreamingApiClient.GetDataFormatManagerClient();
            this.sessionName = sessionName;
            this.streamApiSessionKey = streamApiSessionKey;
            this.dataSource = dataSource;
            this.lastUpdated = DateTime.Now;
        }

        /// <summary>
        /// Subscribes to a stream for which all packets can be read from.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <param name="connectionDetails">Connection Details for the Stream API to read from.</param>
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
                            lastUpdated = DateTime.Now;
                            var packetResponse = packetStream.ResponseStream.Current;
                            foreach (var response in packetResponse.Response) HandleNewPacket(response.Packet);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to read packet stream.");
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Handles the new packet received, deserializes it to its appropriate packet type, and gives it off to the appropriate handler.
        /// </summary>
        /// <param name="packet"></param>
        public void HandleNewPacket(Packet packet)
        {
            
            if (packet.SessionKey != streamApiSessionKey)
            {
                Console.WriteLine("Session Key does not match. Ignoring the packet.");
                return;
            }

            var packetType = packet.Type + "Packet";
            var content = packet.Content;
            try
            {
                switch (packetType)
                {
                    case "PeriodicDataPacket":
                        {
                            Console.WriteLine("Received a periodic packet");
                            var periodicDataPacket = PeriodicDataPacket.Parser.ParseFrom(content);
                            HandlePeriodicPacket(periodicDataPacket);
                            break;
                        }
                    case "RowDataPacket":
                        {
                            Console.WriteLine("Received a row packet");
                            var rowDataPacket = RowDataPacket.Parser.ParseFrom(content);
                            HandleRowData(rowDataPacket);
                            break;
                        }
                    case "MarkerPacket":
                        {
                            Console.WriteLine("Received a marker packet");
                            var markerPacket = MarkerPacket.Parser.ParseFrom(content);
                            HandleMarkerPacket(markerPacket);
                            break;
                        }
                    case "MetadataPacket":
                        {
                            Console.WriteLine("Received a metadata packet");
                            var metadataPacket = MetadataPacket.Parser.ParseFrom(content);
                            HandleMetadataPacket(metadataPacket);
                            break;
                        }
                    case "EventPacket":
                        {
                            Console.WriteLine("Received a event packet");
                            var eventPacket = EventPacket.Parser.ParseFrom(content);
                            HandleEventPacket(eventPacket);
                            break;
                        }
                    default:
                        {
                            Console.WriteLine($"Unable to parse packet {packetType}.");
                            return;
                        }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to handle packet {packetType} due to {ex.Message}");
            }
        }

        /// <summary>
        /// Method to handle periodic packets.
        /// </summary>
        /// <param name="packet"></param>
        private void HandlePeriodicPacket(PeriodicDataPacket packet)
        {
            RepeatedField<string> parameterList;
            if (packet.DataFormat.HasDataFormatIdentifier)
                parameterList = dataFormatManagerServiceClient.GetParametersList(new GetParametersListRequest()
                { DataFormatIdentifier = packet.DataFormat.DataFormatIdentifier, DataSource = dataSource })
                    .Parameters;
            else
                parameterList = packet.DataFormat.ParameterIdentifiers.ParameterIdentifiers;
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
                                parametersPeriodicRecords.Add(new ParameterText()
                                {
                                    Name = parameterIdentifier,
                                    Timestamp = (long)(packet.StartTime + packet.Interval * (uint)j),
                                    Value = dataSample.Value
                                });
                            }
                            break;
                        }
                    case SampleColumn.ListOneofCase.Int32Samples:
                        {
                            for (var j = 0; j < column.Int32Samples.Samples.Count; j++)
                            {
                                var dataSample = column.Int32Samples.Samples[j];
                                parametersPeriodicRecords.Add(new ParameterText()
                                {
                                    Name = parameterIdentifier,
                                    Timestamp = (long)(packet.StartTime + packet.Interval * (uint)j),
                                    Value = dataSample.Value
                                });
                            }
                            break;
                        }
                    case SampleColumn.ListOneofCase.BoolSamples:
                        {
                            for (var j = 0; j < column.BoolSamples.Samples.Count; j++)
                            {
                                var dataSample = column.BoolSamples.Samples[j];
                                parametersPeriodicRecords.Add(new ParameterText()
                                {
                                    Name = parameterIdentifier,
                                    Timestamp = (long)(packet.StartTime + packet.Interval * (uint)j),
                                    Value = dataSample.Value ? 1.0 : 0.0
                                });
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
            }
        }

        /// <summary>
        /// Handles Row Data Packets.
        /// </summary>
        /// <param name="packet"></param>
        private void HandleRowData(RowDataPacket packet)
        {
            RepeatedField<string> parameterList;
            if (packet.DataFormat.HasDataFormatIdentifier)
                parameterList = dataFormatManagerServiceClient.GetParametersList(new GetParametersListRequest()
                { DataFormatIdentifier = packet.DataFormat.DataFormatIdentifier, DataSource = dataSource })
                    .Parameters;
            else
                parameterList = packet.DataFormat.ParameterIdentifiers.ParameterIdentifiers;

            for (var i = 0; i < packet.Rows.Count; i++)
            {
                var row = packet.Rows[i];
                switch (row.ListCase)
                {
                    case SampleRow.ListOneofCase.DoubleSamples:
                        {
                            for (var j = 0; j < row.DoubleSamples.Samples.Count; j++)
                            {
                                parametersRowRecords.Add(new ParameterText()
                                {
                                    Name = parameterList[j],
                                    Timestamp = (long)packet.Timestamps[i],
                                    Value = row.DoubleSamples.Samples[j].Value
                                });
                            }
                            break;
                        }
                    case SampleRow.ListOneofCase.Int32Samples:
                        {
                            for (var j = 0; j < row.Int32Samples.Samples.Count; j++)
                            {
                                parametersRowRecords.Add(new ParameterText()
                                {
                                    Name = parameterList[j],
                                    Timestamp = (long)packet.Timestamps[i],
                                    Value = row.DoubleSamples.Samples[j].Value
                                });
                            }
                            break;
                        }
                    case SampleRow.ListOneofCase.BoolSamples:
                        {
                            for (var j = 0; j < row.BoolSamples.Samples.Count; j++)
                            {
                                parametersRowRecords.Add(new ParameterText()
                                {
                                    Name = parameterList[j],
                                    Timestamp = (long)packet.Timestamps[i],
                                    Value = row.DoubleSamples.Samples[j].Value
                                });
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
        }

        /// <summary>
        /// Handles Marker Packets.
        /// </summary>
        /// <param name="packet"></param>
        private void HandleMarkerPacket(MarkerPacket packet)
        {
            lapsRecords.Add(new LapText()
            {
                Name = packet.Label, Timestamp = packet.Timestamp, Type = packet.Type, Value = packet.Value,
                Description = packet.Description, Source = packet.Source
            });
        }

        /// <summary>
        /// Handles Metadata packets (Session Details).
        /// </summary>
        /// <param name="packet"></param>
        private void HandleMetadataPacket(MetadataPacket packet)
        {
            Console.WriteLine("Metadata packets are unsupported.");
        }

        /// <summary>
        /// Handles Event packets.
        /// </summary>
        /// <param name="packet"></param>
        private void HandleEventPacket(EventPacket packet)
        {
            string eventIdentifier;
            if (packet.DataFormat.HasDataFormatIdentifier)
                eventIdentifier = dataFormatManagerServiceClient.GetEvent(new GetEventRequest()
                { DataFormatIdentifier = packet.DataFormat.DataFormatIdentifier, DataSource = dataSource }).Event;
            else
                eventIdentifier = packet.DataFormat.EventIdentifier;

            var timestamp = (long)packet.Timestamp;
            eventsRecords.Add(new EventText()
            {
                EventIdentifier = eventIdentifier,
                Timestamp = timestamp,
                Data1 = packet.RawValues[0],
                Data2 = packet.RawValues[1],
                Data3 = packet.RawValues[2]
            });
        }

        /// <summary>
        /// Ends the session by writing all decoded packets to a csv file.
        /// </summary>
        public void EndSession()
        {
            // Wait for streams to finish.
            do
            {
            } while (DateTime.Now - lastUpdated < TimeSpan.FromSeconds(10));
            var records = parametersPeriodicRecords;
            records.AddRange(parametersRowRecords);
            this.parameterCsv = this.rootFolderPath + $"{sessionName}_parameters.csv";
            this.eventCsv = rootFolderPath + $"{sessionName}_events.csv";
            this.markersCsv = rootFolderPath + $"{sessionName}_markers.csv";
            using (var writer = new StreamWriter(eventCsv))
            {
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {
                    csv.WriteRecords(eventsRecords);
                }
            }
            using (var writer = new StreamWriter(parameterCsv))
            {
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {
                    csv.WriteRecords(records);
                }
            }
            using (var writer = new StreamWriter(markersCsv))
            {
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {
                    csv.WriteRecords(lapsRecords);
                }
            }
        }
    }
}
