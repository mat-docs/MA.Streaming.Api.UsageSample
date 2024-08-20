// <copyright file="Session.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using System.Collections.Concurrent;
using Google.Protobuf.Collections;
using Grpc.Core;
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


        public void EndSession()
        {
            // Wait for writing to session to end.
            do
            {
            } while (DateTime.Now - lastUpdated < TimeSpan.FromSeconds(120));

            sessionWriter.CloseSession(clientSession);
        }

        public void ReadPackets(CancellationToken cancellationToken, Connection connectionDetails)
        {
            var streamReader = CreateStream(connectionDetails)?.ResponseStream;

            if (streamReader == null)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    while (await streamReader.MoveNext(cancellationToken))
                    {
                        var packetResponse = streamReader.Current;
                        foreach (var response in packetResponse.Response) await HandleNewPacket(response.Packet);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to read stream due to {ex}");
                }
                finally
                {
                    ReadPackets(cancellationToken, connectionDetails);
                }
            }, cancellationToken);
            
        }

        private AsyncServerStreamingCall<ReadPacketsResponse>? CreateStream(
            Connection connectionDetails)
        {
            return packetReaderServiceClient.ReadPackets(new ReadPacketsRequest()
                { Connection = connectionDetails });
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
                HandleRowData(packet);
        }

        private void ConfigProcessor_ProcessPeriodicComplete(object? sender, EventArgs e)
        {
            var dataQueue = periodicDataQueue.ToArray();
            periodicDataQueue.Clear();
            foreach (var packet in dataQueue)
                HandlePeriodicPacket(packet);
        }

        //public void GetEssentialPacket(CancellationToken cancellationToken, Connection connectionDetails)
        //{
        //    var essentialsPacketStream = packetReaderServiceClient.ReadEssentials(new ReadEssentialsRequest()
        //        { Connection = connectionDetails }).ResponseStream;
        //    var task = Task.Run(async () =>
        //    {
        //        try
        //        {
        //            while (!cancellationToken.IsCancellationRequested)
        //            {
        //                while (await essentialsPacketStream.MoveNext(cancellationToken))
        //                {
        //                    var messages = essentialsPacketStream.Current.Response;

        //                    foreach (var message in messages)
        //                    {
        //                        Console.WriteLine($"ESSENTIALS : New Packet {message.Packet.Type}");
        //                        HandleNewPacket(message.Packet);
        //                    }
        //                }

        //                return;
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            Console.WriteLine($"Unable to get essentials packet due to {ex.Message}.");
        //        }
        //    }, cancellationToken);
        //}

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
                        HandlePeriodicPacket(periodicDataPacket);
                        break;
                    }
                    case "RowData":
                    {
                        var rowDataPacket = RowDataPacket.Parser.ParseFrom(content);
                        HandleRowData(rowDataPacket);
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

        private void HandlePeriodicPacket(PeriodicDataPacket packet)
        {
            RepeatedField<string> parameterList;
            try
            {
                parameterList = packet.DataFormat.HasDataFormatIdentifier
                    ? GetParameterList(packet.DataFormat.DataFormatIdentifier)
                    : packet.DataFormat.ParameterIdentifiers.ParameterIdentifiers;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Parameter List Exception {ex}");
                parameterList = new RepeatedField<string>();
            }

            var newParameters = parameterList
                .Where(x => !sessionWriter.channelIdPeriodicParameterDictionary.ContainsKey(x)).ToList();
            if (newParameters.Any())
            {
                // If the packet contains new parameters, put it in the list parameters to add to config and queue the packet to process later.
                foreach (var parameter in newParameters)
                    configProcessor.AddPeriodicPacketParameter(new Tuple<string, uint>(parameter, packet.Interval));
                periodicDataQueue.Enqueue(packet);
                lastUpdated = DateTime.Now;
                return;
            }

            for (var i = 0; i < parameterList.Count; i++)
            {
                var parameterIdentifier = parameterList[i];
                var column = packet.Columns[i];
                lastUpdated = DateTime.Now;
                switch (column.ListCase)
                {
                    case SampleColumn.ListOneofCase.DoubleSamples:
                    {
                        var samples = column.DoubleSamples.Samples.Select(x => x.Value).ToList();
                        if (sessionWriter.TryAddPeriodicData(clientSession, parameterIdentifier,
                                samples, (long)packet.StartTime % NumberOfNanosecondsInDay))
                        {
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
        }

        private void HandleRowData(RowDataPacket packet)
        {
            RepeatedField<string> parameterList;
            try
            { 
                parameterList = packet.DataFormat.HasDataFormatIdentifier
                    ? GetParameterList(packet.DataFormat.DataFormatIdentifier)
                    : packet.DataFormat.ParameterIdentifiers.ParameterIdentifiers;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Parameter List Exception {ex}");
                parameterList = new RepeatedField<string>();
            }
            

            var newParameters = parameterList.Where(x => !sessionWriter.channelIdParameterDictionary.ContainsKey(x))
                .ToList();
            if (newParameters.Any())
            {
                // If the packet contains new parameters, put it in the parameter list to add to config and queue the packet to process later.
                configProcessor.AddPacketParameter(newParameters);
                rowDataQueue.Enqueue(packet);
                lastUpdated = DateTime.Now;
                return;
            }

            for (var i = 0; i < packet.Rows.Count; i++)
            {
                var row = packet.Rows[i];
                var timestamp = (long)packet.Timestamps[i] % NumberOfNanosecondsInDay;
                lastUpdated = DateTime.Now;

                switch (row.ListCase)
                {
                    case SampleRow.ListOneofCase.DoubleSamples:
                    {
                        if (sessionWriter.TryAddData(clientSession, parameterList,
                                row.DoubleSamples.Samples.Select(x => x.Value).ToList(), timestamp))
                        {
                            continue;
                        }
                        Console.WriteLine($"Failed to write data.");
                        rowDataQueue.Enqueue(packet);
                        break;
                    }
                    case SampleRow.ListOneofCase.Int32Samples:
                    {
                        if (sessionWriter.TryAddData(clientSession, parameterList,
                                row.Int32Samples.Samples.Select(x => (double)x.Value).ToList(), timestamp))
                        {
                            continue;
                        }
                        Console.WriteLine($"Failed to write data.");
                        rowDataQueue.Enqueue(packet);
                        break;
                    }
                    case SampleRow.ListOneofCase.BoolSamples:
                    {
                        if (sessionWriter.TryAddData(clientSession, parameterList,
                                row.BoolSamples.Samples.Select(x => x.Value ? 1.0 : 0.0).ToList(), timestamp))
                        {
                            continue;
                        }
                        Console.WriteLine($"Failed to write data.");
                        rowDataQueue.Enqueue(packet);
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

        private void HandleMarkerPacket(MarkerPacket packet)
        {
            var timestamp = (long)packet.Timestamp % NumberOfNanosecondsInDay;
            if (packet.Type == "Lap Trigger")
                sessionWriter.AddLap(clientSession, timestamp, (short)packet.Value, packet.Label, true);
            else
                sessionWriter.AddMarker(clientSession, timestamp, packet.Label);
            lastUpdated = DateTime.Now;
        }

        private void HandleMetadataPacket(MetadataPacket packet)
        {
            Console.WriteLine("Metadata packets are unsupported.");
        }

        private void HandleEventPacket(EventPacket packet)
        {
            var eventIdentifier = packet.DataFormat.HasDataFormatIdentifier
                ? GetEventIdentifier(packet.DataFormat.DataFormatIdentifier)
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
            lastUpdated = DateTime.Now;
            if (sessionWriter.TryAddEvent(clientSession, eventIdentifier, timestamp, values))
            {
                return;
            }
            eventPacketQueue.Enqueue(packet);
            Console.WriteLine($"Failed to write event {eventIdentifier}.");
        }

        private RepeatedField<string> GetParameterList(ulong dataFormatId)
        {
            if (parameterListDataFormatCache.TryGetValue(dataFormatId, out RepeatedField<string>? parameterList))
                return parameterList;

            parameterList = dataFormatManagerServiceClient.GetParametersList(new GetParametersListRequest()
            {
                DataFormatIdentifier = dataFormatId,
                DataSource = dataSource
            }).Parameters;

            parameterListDataFormatCache[dataFormatId] = parameterList;

            return parameterList;
        }

        private string GetEventIdentifier(ulong dataFormatId)
        {
            if (eventIdentifierDataFormatCache.TryGetValue(dataFormatId, out string? eventIdentifier))
                return eventIdentifier;

            eventIdentifier = dataFormatManagerServiceClient.GetEvent(new GetEventRequest()
            {
                DataFormatIdentifier = dataFormatId,
                DataSource = dataSource
            }).Event;
            eventIdentifierDataFormatCache[dataFormatId] = eventIdentifier;

            return eventIdentifier;
        }
    }
}