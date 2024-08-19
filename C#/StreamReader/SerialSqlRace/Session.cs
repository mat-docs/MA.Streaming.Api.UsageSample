// <copyright file="Session.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using System.Collections.Concurrent;
using Google.Protobuf.Collections;
using MA.Streaming.API;
using MA.Streaming.OpenData;
using MA.Streaming.Proto.Client.Remote;
using MESL.SqlRace.Domain;

namespace Stream.Api.Stream.Reader.SerialSqlRace
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
        private readonly ConfigurationProcessor configProcessor;
        private readonly ConcurrentDictionary<ulong, string> eventIdentifierDataFormatCache = new();
        private readonly ConcurrentQueue<EventPacket> eventPacketQueue = new();

        private DateTime lastUpdated;
        private readonly ConcurrentDictionary<ulong, RepeatedField<string>> parameterListDataFormatCache = new();
        private readonly ConcurrentQueue<PeriodicDataPacket> periodicDataQueue = new();
        private readonly ConcurrentQueue<RowDataPacket> rowDataQueue = new();

        public Session(IClientSession clientSession,
            AtlasSessionWriter sessionWriter, string dataSource)
        {
            this.clientSession = clientSession;
            packetReaderServiceClient = RemoteStreamingApiClient.GetPacketReaderClient();
            dataFormatManagerServiceClient = RemoteStreamingApiClient.GetDataFormatManagerClient();
            this.sessionWriter = sessionWriter;
            this.dataSource = dataSource;
            lastUpdated = DateTime.Now;
            configProcessor = new ConfigurationProcessor(sessionWriter, clientSession);
            configProcessor.ProcessPeriodicComplete += ConfigProcessor_ProcessPeriodicComplete;
            configProcessor.ProcessEventComplete += ConfigProcessor_ProcessEventComplete;
            configProcessor.ProcessComplete += ConfigProcessor_ProcessCompleteRow;
        }

        public void UpdateSessionInfo(GetSessionInfoResponse sessionInfo)
        {
            sessionWriter.UpdateSessionInfo(clientSession, sessionInfo);
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
                    while (await packetStream.ResponseStream.MoveNext(cancellationToken))
                    {
                        var packetResponse = packetStream.ResponseStream.Current;
                        foreach (var response in packetResponse.Response) HandleNewPacket(response.Packet);
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
            } while (DateTime.Now - lastUpdated < TimeSpan.FromSeconds(120));

            sessionWriter.CloseSession(clientSession);
        }

        private void ConfigProcessor_ProcessEventComplete(object? sender, EventArgs e)
        {
            var dataQueue = eventPacketQueue.ToArray();
            eventPacketQueue.Clear();
            foreach (var packet in dataQueue) HandleEventPacket(packet);
        }

        private void ConfigProcessor_ProcessCompleteRow(object? sender, EventArgs e)
        {
            var dataQueue = rowDataQueue.ToArray();
            rowDataQueue.Clear();
            foreach (var packet in dataQueue)
                Task.Run(async () => { HandleRowData(packet); });
        }

        private void ConfigProcessor_ProcessPeriodicComplete(object? sender, EventArgs e)
        {
            var dataQueue = periodicDataQueue.ToArray();
            periodicDataQueue.Clear();
            foreach (var packet in dataQueue)
                Task.Run(async () => { HandlePeriodicPacket(packet); });
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
                            }
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

        public Task HandleNewPacket(Packet packet)
        {
            var packetType = packet.Type;
            var content = packet.Content;
            try
            {
                switch (packetType)
                {
                    case "Configuration":
                    {
                        var packetConfig = ConfigurationPacket.Parser.ParseFrom(content);
                        sessionWriter.AddConfiguration(clientSession, packetConfig);
                        break;
                    }
                    case "PeriodicData":
                    {
                        var periodicDataPacket = PeriodicDataPacket.Parser.ParseFrom(content);
                        Task.Run(async () => { HandlePeriodicPacket(periodicDataPacket); });

                        break;
                    }
                    case "RowData":
                    {
                        var rowDataPacket = RowDataPacket.Parser.ParseFrom(content);
                        Task.Run(async () => { HandleRowData(rowDataPacket); });
                        break;
                    }
                    case "Marker":
                    {
                        var markerPacket = MarkerPacket.Parser.ParseFrom(content);
                        HandleMarkerPacket(markerPacket);
                        break;
                    }
                    case "Metadata":
                    {
                        var metadataPacket = MetadataPacket.Parser.ParseFrom(content);
                        HandleMetadataPacket(metadataPacket);
                        break;
                    }
                    case "Event":
                    {
                        var eventPacket = EventPacket.Parser.ParseFrom(content);
                        HandleEventPacket(eventPacket);
                        break;
                    }
                    default:
                    {
                        Console.WriteLine($"Unable to parse packet {packetType}.");
                        break;
                    }
                }

                lastUpdated = DateTime.Now;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to handle packet {packetType} due to {ex.Message}");
            }

            return Task.CompletedTask;
        }

        private Task HandlePeriodicPacket(PeriodicDataPacket packet)
        {
            var parameterList = packet.DataFormat.HasDataFormatIdentifier
                ? GetParameterListAsync(packet.DataFormat.DataFormatIdentifier).Result
                : packet.DataFormat.ParameterIdentifiers.ParameterIdentifiers;

            var newParameters = parameterList
                .Where(x => !sessionWriter.channelIdPeriodicParameterDictionary.ContainsKey(x)).ToList();
            if (newParameters.Any())
            {
                // If the packet contains new parameters, put it in the list parameters to add to config and queue the packet to process later.
                foreach (var parameter in newParameters)
                    configProcessor.AddPeriodicPacketParameter(new Tuple<string, uint>(parameter, packet.Interval));
                periodicDataQueue.Enqueue(packet);
                lastUpdated = DateTime.Now;
                return Task.CompletedTask;
            }

            for (var i = 0; i < parameterList.Count; i++)
            {
                var parameterIdentifier = parameterList[i];
                var column = packet.Columns[i];
                switch (column.ListCase)
                {
                    case SampleColumn.ListOneofCase.DoubleSamples:
                    {
                        var samples = column.DoubleSamples.Samples.Select(x => x.Value).ToList();
                        if (sessionWriter.TryAddPeriodicData(clientSession, parameterIdentifier,
                                samples, (long)packet.StartTime % NumberOfNanosecondsInDay))
                        {
                            lastUpdated = DateTime.Now;
                            break;
                        }

                        Console.WriteLine($"Failed to write data for periodic parameter {parameterIdentifier}.");
                        periodicDataQueue.Enqueue(packet);
                        break;
                    }
                    case SampleColumn.ListOneofCase.Int32Samples:
                    {
                        var samples = column.Int32Samples.Samples.Select(x => (double)x.Value).ToList();
                        if (sessionWriter.TryAddPeriodicData(clientSession, parameterIdentifier,
                                samples, (long)packet.StartTime % NumberOfNanosecondsInDay))
                        {
                            lastUpdated = DateTime.Now;
                            break;
                        }

                        Console.WriteLine($"Failed to write data for periodic parameter {parameterIdentifier}.");
                        periodicDataQueue.Enqueue(packet);

                        break;
                    }
                    case SampleColumn.ListOneofCase.BoolSamples:
                    {
                        var samples = column.BoolSamples.Samples.Select(x => x.Value ? 1.0 : 0.0).ToList();
                        if (sessionWriter.TryAddPeriodicData(clientSession, parameterIdentifier,
                                samples, (long)packet.StartTime % NumberOfNanosecondsInDay))
                        {
                            lastUpdated = DateTime.Now;
                            break;
                        }

                        Console.WriteLine($"Failed to write data for periodic parameter {parameterIdentifier}.");
                        periodicDataQueue.Enqueue(packet);

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

            return Task.CompletedTask;
        }

        private Task HandleRowData(RowDataPacket packet)
        {
            var parameterList = packet.DataFormat.HasDataFormatIdentifier
                ? GetParameterListAsync(packet.DataFormat.DataFormatIdentifier).Result
                : packet.DataFormat.ParameterIdentifiers.ParameterIdentifiers;

            var newParameters = parameterList.Where(x => !sessionWriter.channelIdParameterDictionary.ContainsKey(x))
                .ToList();
            if (newParameters.Any())
            {
                // If the packet contains new parameters, put it in the parameter list to add to config and queue the packet to process later.
                configProcessor.AddPacketParameter(newParameters);
                rowDataQueue.Enqueue(packet);
                lastUpdated = DateTime.Now;
                return Task.CompletedTask;
            }

            for (var i = 0; i < packet.Rows.Count; i++)
            {
                var row = packet.Rows[i];
                var timestamp = (long)packet.Timestamps[i] % NumberOfNanosecondsInDay;

                switch (row.ListCase)
                {
                    case SampleRow.ListOneofCase.DoubleSamples:
                    {
                        for (var j = 0; j < row.DoubleSamples.Samples.Count; j++)
                        {
                            lastUpdated = DateTime.Now;
                            if (sessionWriter.TryAddData(clientSession, parameterList[j],
                                    row.DoubleSamples.Samples[j].Value, timestamp)) continue;
                            Console.WriteLine($"Failed to write data to parameter {parameterList[j]}");
                            rowDataQueue.Enqueue(packet);
                        }

                        break;
                    }
                    case SampleRow.ListOneofCase.Int32Samples:
                    {
                        for (var j = 0; j < row.Int32Samples.Samples.Count; j++)
                        {
                            lastUpdated = DateTime.Now;
                            if (sessionWriter.TryAddData(clientSession, parameterList[j],
                                    row.Int32Samples.Samples[j].Value, timestamp)) continue;
                            Console.WriteLine($"Failed to write data to parameter {parameterList[j]}");
                            rowDataQueue.Enqueue(packet);
                        }

                        break;
                    }
                    case SampleRow.ListOneofCase.BoolSamples:
                    {
                        for (var j = 0; j < row.BoolSamples.Samples.Count; j++)
                        {
                            lastUpdated = DateTime.Now;
                            if (sessionWriter.TryAddData(clientSession, parameterList[j],
                                    row.BoolSamples.Samples[j].Value ? 1.0 : 0.0, timestamp)) continue;
                            Console.WriteLine($"Failed to write data to parameter {parameterList[j]}");
                            rowDataQueue.Enqueue(packet);
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

            return Task.CompletedTask;
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
            var eventIdentifier = packet.DataFormat.HasDataFormatIdentifier
                ? GetEventIdentifier(packet.DataFormat.DataFormatIdentifier).Result
                : packet.DataFormat.EventIdentifier;

            if (!sessionWriter.eventDefCache.ContainsKey(eventIdentifier))
            {
                configProcessor.AddPacketEvent(eventIdentifier);
                eventPacketQueue.Enqueue(packet);
                return;
            }

            var timestamp = (long)packet.Timestamp % NumberOfNanosecondsInDay;
            var values = new List<double>();
            values.AddRange(packet.RawValues);
            sessionWriter.TryAddEvent(clientSession, eventIdentifier, timestamp, values);
        }

        private async Task<RepeatedField<string>> GetParameterListAsync(ulong dataFormatId)
        {
            if (parameterListDataFormatCache.TryGetValue(dataFormatId, out RepeatedField<string>? parameterList))
                return parameterList;

            parameterList = dataFormatManagerServiceClient.GetParametersListAsync(new GetParametersListRequest()
            {
                DataFormatIdentifier = dataFormatId,
                DataSource = dataSource
            }).ResponseAsync.Result.Parameters;
            parameterListDataFormatCache[dataFormatId] = parameterList;

            return parameterList;
        }

        private async Task<string> GetEventIdentifier(ulong dataFormatId)
        {
            if (eventIdentifierDataFormatCache.TryGetValue(dataFormatId, out string? eventIdentifier))
                return eventIdentifier;

            eventIdentifier = dataFormatManagerServiceClient.GetEventAsync(new GetEventRequest()
            {
                DataFormatIdentifier = dataFormatId,
                DataSource = dataSource
            }).ResponseAsync.Result.Event;
            eventIdentifierDataFormatCache[dataFormatId] = eventIdentifier;

            return eventIdentifier;
        }
    }
}