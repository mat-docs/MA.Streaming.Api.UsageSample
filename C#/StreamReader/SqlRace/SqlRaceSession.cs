// <copyright file="SqlRaceSession.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using System.Collections.Concurrent;
using Grpc.Core;
using MA.Streaming.API;
using MA.Streaming.OpenData;
using MESL.SqlRace.Domain;
using Stream.Api.Stream.Reader.Handlers;
using ISession = Stream.Api.Stream.Reader.Interfaces.ISession;

namespace Stream.Api.Stream.Reader.SqlRace
{
    internal class SqlRaceSession : ISession
    {
        private IClientSession _clientSession;
        private readonly AtlasSessionWriter sessionWriter;
        private ConfigurationProcessor configProcessor;
        private readonly ConcurrentQueue<EventPacket> eventPacketQueue = new();
        private readonly StreamApiClient streamApiClient;
        private string sessionKey;
        private Dictionary<string, long> streamsOffsetDictionary = new();
        private readonly CancellationTokenSource cancellationTokenSource = new();
        private List<Connection> connections = new();

        private DateTime _lastUpdated;
        private readonly ConcurrentQueue<PeriodicDataPacket> periodicDataQueue = new();
        private readonly ConcurrentQueue<RowDataPacket> rowDataQueue = new();
        private PeriodicDataHandler periodicDataHandler;
        private RowDataHandler rowDataHandler;
        private MarkerHandler markerHandler;
        private EventDataHandler eventDataHandler;

        public bool SessionEnded { get; private set; }

        public SqlRaceSession(AtlasSessionWriter sessionWriter, StreamApiClient streamApiClient)
        {
            this.sessionWriter = sessionWriter;
            _lastUpdated = DateTime.Now;
            this.streamApiClient = streamApiClient;
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
            } while (DateTime.Now - _lastUpdated < TimeSpan.FromSeconds(60));

            sessionWriter.CloseSession(_clientSession);
            connections.ForEach(x => streamApiClient.TryCloseConnection(x));
            this.SessionEnded = true;
        }

        public void StartSession(string sessionKey)
        {
            Console.WriteLine($"New Live SqlRaceSession found with key {sessionKey}.");
            var sessionInfo = streamApiClient.GetSessionInfo(sessionKey);
            var sqlSession = sessionWriter.CreateSession(
                sessionInfo.Identifier == "" ? "Untitled" : sessionInfo.Identifier,
                sessionInfo.Type);
            if (sqlSession == null)
            {
                Console.WriteLine("Failed to create session.");
                return;
            }

            this.SessionEnded = false;
            this._clientSession = sqlSession;
            this.sessionKey = sessionKey;

            

            configProcessor = new ConfigurationProcessor(sessionWriter, sqlSession);
            this.periodicDataHandler =
                new PeriodicDataHandler(sessionWriter, streamApiClient, sqlSession, configProcessor);
            this.rowDataHandler = new RowDataHandler(sessionWriter, streamApiClient, sqlSession, configProcessor);
            this.markerHandler = new MarkerHandler(sessionWriter, streamApiClient, sqlSession);
            this.eventDataHandler = new EventDataHandler(sessionWriter, streamApiClient, sqlSession, configProcessor);
            configProcessor.ProcessPeriodicComplete += OnProcessPeriodicComplete;
            configProcessor.ProcessEventComplete += OnProcessorProcessEventComplete;
            configProcessor.ProcessRowComplete += OnProcessRowComplete;
            Task.Run(UpdateSessionInfo);
        }

        private void UpdateSessionInfo()
        {
            bool isComplete;
            do
            {
                var sessionInfo = streamApiClient.GetSessionInfo(sessionKey);
                isComplete = sessionInfo.IsComplete;
                var newStreams = sessionInfo.Streams.Where(x => !streamsOffsetDictionary.ContainsKey(x)).ToList();
                if (newStreams.Any())
                {
                    var connectionDetails = new ConnectionDetails
                    {
                        DataSource = sessionInfo.DataSource,
                        EssentialsOffset = sessionInfo.EssentialsOffset,
                        MainOffset = sessionInfo.MainOffset,
                        Session = sessionKey,
                        StreamOffsets =
                        {
                            newStreams.Select(x =>
                                sessionInfo.TopicPartitionOffsets.GetValueOrDefault($"{sessionInfo.DataSource}.{x}:[0]",
                                    0)).ToList()
                        },
                        Streams = { newStreams }
                    };
                    var connection = streamApiClient.GetNewConnectionToSession(connectionDetails);
                    this.ReadPackets(cancellationTokenSource.Token, connection);
                    this.connections.Add(connection);
                }
                this.UpdateSessionInfo(sessionInfo);
                Task.Delay(TimeSpan.FromSeconds(1)).Wait();
            } while (!isComplete);
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
            }, cancellationToken);
            
        }
        private Task HandleNewPacket(Packet packet)
        {
            var packetType = packet.Type;
            var content = packet.Content;
            _lastUpdated = DateTime.Now;
            try
            {
                switch (packetType)
                {
                    case "PeriodicData":
                        {
                            var periodicDataPacket = PeriodicDataPacket.Parser.ParseFrom(content);
                            if (!this.periodicDataHandler.TryHandle(periodicDataPacket))
                            {
                                periodicDataQueue.Enqueue(periodicDataPacket);
                            }
                            break;
                        }
                    case "RowData":
                        {
                            var rowDataPacket = RowDataPacket.Parser.ParseFrom(content);
                            if (!this.rowDataHandler.TryHandle(rowDataPacket))
                            {
                                rowDataQueue.Enqueue(rowDataPacket);
                            }
                            break;
                        }
                    case "Marker":
                        {
                            var markerPacket = MarkerPacket.Parser.ParseFrom(content);
                            this.markerHandler.TryHandle(markerPacket);
                            break;
                        }
                    case "Event":
                        {
                            var eventPacket = EventPacket.Parser.ParseFrom(content);
                            if (!this.eventDataHandler.TryHandle(eventPacket))
                            {
                                eventPacketQueue.Enqueue(eventPacket);
                            }
                            break;
                        }
                    default:
                        {
                            Console.WriteLine($"Unable to parse packet {packetType}.");
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to handle packet {packetType} due to {ex.Message}");
            }

            return Task.CompletedTask;
        }
        private AsyncServerStreamingCall<ReadPacketsResponse>? CreateStream(
            Connection connectionDetails)
        {
            return streamApiClient.CreateReadPacketsStream(connectionDetails);
        }
        private void OnProcessorProcessEventComplete(object? sender, EventArgs e)
        {
            var dataQueue = eventPacketQueue.ToArray();
            eventPacketQueue.Clear();
            foreach (var packet in dataQueue) this.eventDataHandler.TryHandle(packet);
        }

        private void OnProcessRowComplete(object? sender, EventArgs e)
        {
            var dataQueue = rowDataQueue.ToArray();
            rowDataQueue.Clear();
            foreach (var packet in dataQueue)
                this.rowDataHandler.TryHandle(packet);
        }

        private void OnProcessPeriodicComplete(object? sender, EventArgs e)
        {
            var dataQueue = periodicDataQueue.ToArray();
            periodicDataQueue.Clear();
            foreach (var packet in dataQueue)
                //HandlePeriodicPacket(packet);
                this.periodicDataHandler.TryHandle(packet);
        }
    }
}