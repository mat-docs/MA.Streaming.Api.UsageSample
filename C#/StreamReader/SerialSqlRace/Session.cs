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
        private readonly IClientSession _clientSession;
        private readonly DataFormatManagerService.DataFormatManagerServiceClient _dataFormatManagerServiceClient;
        private readonly string _dataSource;
        private readonly PacketReaderService.PacketReaderServiceClient _packetReaderServiceClient;
        private readonly AtlasSessionWriter _sessionWriter;
        private readonly ConfigurationProcessor _configProcessor;
        private readonly ConcurrentDictionary<ulong, string> _eventIdentifierDataFormatCache = new();
        private readonly ConcurrentQueue<EventPacket> _eventPacketQueue = new();

        private DateTime _lastUpdated;
        private readonly ConcurrentDictionary<ulong, RepeatedField<string>> _parameterListDataFormatCache = new();
        private readonly ConcurrentQueue<PeriodicDataPacket> _periodicDataQueue = new();
        private readonly ConcurrentQueue<RowDataPacket> _rowDataQueue = new();

        public Session(IClientSession clientSession,
            AtlasSessionWriter sessionWriter, string dataSource)
        {
            this._clientSession = clientSession;
            _packetReaderServiceClient = RemoteStreamingApiClient.GetPacketReaderClient();
            _dataFormatManagerServiceClient = RemoteStreamingApiClient.GetDataFormatManagerClient();
            this._sessionWriter = sessionWriter;
            this._dataSource = dataSource;
            _lastUpdated = DateTime.Now;
            _configProcessor = new ConfigurationProcessor(sessionWriter, clientSession);
            _configProcessor.ProcessPeriodicComplete += OnProcessPeriodicComplete;
            _configProcessor.ProcessEventComplete += OnProcessorProcessEventComplete;
            _configProcessor.ProcessRowComplete += OnProcessRowComplete;
        }

        public void UpdateSessionInfo(GetSessionInfoResponse sessionInfo)
        {
            AtlasSessionWriter.UpdateSessionInfo(_clientSession, sessionInfo);
        }

        public void EndSession()
        {
            // Wait for writing to session to end.
            do
            {
                Task.Delay(1000).Wait();
            } while (DateTime.Now - _lastUpdated < TimeSpan.FromSeconds(120));

            AtlasSessionWriter.CloseSession(_clientSession);
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
                    ReadPackets(cancellationToken, connectionDetails);
                }
            }, cancellationToken);
            
        }
        private Task HandleNewPacket(Packet packet)
        {
            var packetType = packet.Type;
            var content = packet.Content;
            try
            {
                switch (packetType)
                {
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

                _lastUpdated = DateTime.Now;
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
                .Where(x => !_sessionWriter.IsParameterExistInConfig(x, packet.Interval))
                .ToList();

            if (newParameters.Any())
            {
                // If the packet contains new parameters, put it in the list parameters to add to config and queue the packet to process later.
                foreach (var parameter in newParameters)
                    _configProcessor.AddPeriodicPacketParameter(new Tuple<string, uint>(parameter, packet.Interval));
                _periodicDataQueue.Enqueue(packet);
                _lastUpdated = DateTime.Now;
                return;
            }

            for (var i = 0; i < parameterList.Count; i++)
            {
                var parameterIdentifier = parameterList[i];
                var column = packet.Columns[i];
                _lastUpdated = DateTime.Now;
                switch (column.ListCase)
                {
                    case SampleColumn.ListOneofCase.DoubleSamples:
                    {
                        var samples = column.DoubleSamples.Samples.Select(x => x.Value).ToList();
                        if (_sessionWriter.TryAddPeriodicData(_clientSession, parameterIdentifier,
                                samples, (long)packet.StartTime % NumberOfNanosecondsInDay, packet.Interval))
                        {
                            break;
                        }

                        Console.WriteLine($"Failed to write data for periodic parameter {parameterIdentifier}.");
                        _periodicDataQueue.Enqueue(packet);
                        break;
                    }
                    case SampleColumn.ListOneofCase.Int32Samples:
                    {
                        var samples = column.Int32Samples.Samples.Select(x => (double)x.Value).ToList();
                        if (_sessionWriter.TryAddPeriodicData(_clientSession, parameterIdentifier,
                                samples, (long)packet.StartTime % NumberOfNanosecondsInDay, packet.Interval))
                        {
                            break;
                        }

                        Console.WriteLine($"Failed to write data for periodic parameter {parameterIdentifier}.");
                        _periodicDataQueue.Enqueue(packet);

                        break;
                    }
                    case SampleColumn.ListOneofCase.BoolSamples:
                    {
                        var samples = column.BoolSamples.Samples.Select(x => x.Value ? 1.0 : 0.0).ToList();
                        if (_sessionWriter.TryAddPeriodicData(_clientSession, parameterIdentifier,
                                samples, (long)packet.StartTime % NumberOfNanosecondsInDay, packet.Interval))
                        {
                            break;
                        }

                        Console.WriteLine($"Failed to write data for periodic parameter {parameterIdentifier}.");
                        _periodicDataQueue.Enqueue(packet);

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

            var newParameters = parameterList
                .Where(x => !_sessionWriter.IsParameterExistInConfig(x))
                .ToList();

            if (newParameters.Any())
            {
                // If the packet contains new parameters, put it in the parameter list to add to config and queue the packet to process later.
                _configProcessor.AddRowPacketParameter(newParameters);
                _rowDataQueue.Enqueue(packet);
                _lastUpdated = DateTime.Now;
                return;
            }

            for (var i = 0; i < packet.Rows.Count; i++)
            {
                var row = packet.Rows[i];
                var timestamp = (long)packet.Timestamps[i] % NumberOfNanosecondsInDay;
                _lastUpdated = DateTime.Now;

                switch (row.ListCase)
                {
                    case SampleRow.ListOneofCase.DoubleSamples:
                    {
                        if (_sessionWriter.TryAddData(_clientSession, parameterList,
                                row.DoubleSamples.Samples.Select(x => x.Value).ToList(), timestamp))
                        {
                            continue;
                        }
                        Console.WriteLine($"Failed to write data.");
                        _rowDataQueue.Enqueue(packet);
                        break;
                    }
                    case SampleRow.ListOneofCase.Int32Samples:
                    {
                        if (_sessionWriter.TryAddData(_clientSession, parameterList,
                                row.Int32Samples.Samples.Select(x => (double)x.Value).ToList(), timestamp))
                        {
                            continue;
                        }
                        Console.WriteLine($"Failed to write data.");
                        _rowDataQueue.Enqueue(packet);
                        break;
                    }
                    case SampleRow.ListOneofCase.BoolSamples:
                    {
                        if (_sessionWriter.TryAddData(_clientSession, parameterList,
                                row.BoolSamples.Samples.Select(x => x.Value ? 1.0 : 0.0).ToList(), timestamp))
                        {
                            continue;
                        }
                        Console.WriteLine($"Failed to write data.");
                        _rowDataQueue.Enqueue(packet);
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
                _sessionWriter.AddLap(_clientSession, timestamp, (short)packet.Value, packet.Label, true);
            else
                _sessionWriter.AddMarker(_clientSession, timestamp, packet.Label);
            _lastUpdated = DateTime.Now;
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

            if (!_sessionWriter.EventDefCache.ContainsKey(eventIdentifier))
            {
                _configProcessor.AddPacketEvent(eventIdentifier);
                _eventPacketQueue.Enqueue(packet);
                return;
            }

            var timestamp = (long)packet.Timestamp % NumberOfNanosecondsInDay;
            var values = new List<double>();
            values.AddRange(packet.RawValues);
            _lastUpdated = DateTime.Now;
            if (_sessionWriter.TryAddEvent(_clientSession, eventIdentifier, timestamp, values))
            {
                return;
            }
            _eventPacketQueue.Enqueue(packet);
            Console.WriteLine($"Failed to write event {eventIdentifier}.");
        }

        private RepeatedField<string> GetParameterList(ulong dataFormatId)
        {
            if (_parameterListDataFormatCache.TryGetValue(dataFormatId, out RepeatedField<string>? parameterList))
                return parameterList;

            parameterList = _dataFormatManagerServiceClient.GetParametersList(new GetParametersListRequest()
            {
                DataFormatIdentifier = dataFormatId,
                DataSource = _dataSource
            }).Parameters;

            _parameterListDataFormatCache[dataFormatId] = parameterList;

            return parameterList;
        }
        private AsyncServerStreamingCall<ReadPacketsResponse>? CreateStream(
            Connection connectionDetails)
        {
            return _packetReaderServiceClient.ReadPackets(new ReadPacketsRequest()
                { Connection = connectionDetails });
        }
        private string GetEventIdentifier(ulong dataFormatId)
        {
            if (_eventIdentifierDataFormatCache.TryGetValue(dataFormatId, out string? eventIdentifier))
                return eventIdentifier;

            eventIdentifier = _dataFormatManagerServiceClient.GetEvent(new GetEventRequest()
            {
                DataFormatIdentifier = dataFormatId,
                DataSource = _dataSource
            }).Event;
            _eventIdentifierDataFormatCache[dataFormatId] = eventIdentifier;

            return eventIdentifier;
        }
        private void OnProcessorProcessEventComplete(object? sender, EventArgs e)
        {
            var dataQueue = _eventPacketQueue.ToArray();
            _eventPacketQueue.Clear();
            foreach (var packet in dataQueue) HandleEventPacket(packet);
        }

        private void OnProcessRowComplete(object? sender, EventArgs e)
        {
            var dataQueue = _rowDataQueue.ToArray();
            _rowDataQueue.Clear();
            foreach (var packet in dataQueue)
                HandleRowData(packet);
        }

        private void OnProcessPeriodicComplete(object? sender, EventArgs e)
        {
            var dataQueue = _periodicDataQueue.ToArray();
            _periodicDataQueue.Clear();
            foreach (var packet in dataQueue)
                HandlePeriodicPacket(packet);
        }
    }
}