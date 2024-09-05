// <copyright file="SqlRaceSession.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using System.Collections.Concurrent;
using Grpc.Core;
using MA.Streaming.API;
using MA.Streaming.OpenData;
using MESL.SqlRace.Domain;
using Stream.Api.Stream.Reader.Handlers;
using ISession = Stream.Api.Stream.Reader.Abstractions.ISession;

namespace Stream.Api.Stream.Reader.SqlRace
{
    internal class SqlRaceSession : ISession
    {
        private IClientSession clientSession;
        private readonly AtlasSessionWriter sessionWriter;
        private ConfigurationProcessor configProcessor;
        private readonly ConcurrentQueue<EventPacket> eventPacketQueue = new();
        private readonly StreamApiClient streamApiClient;
        private string sessionKey;
        private Dictionary<string, long> streamsOffsetDictionary = new();
        private readonly CancellationTokenSource cancellationTokenSource = new();
        private List<Connection?> connections = new();

        private DateTime lastUpdated;
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
            this.lastUpdated = DateTime.Now;
            this.streamApiClient = streamApiClient;
        }

        public void UpdateSessionInfo(GetSessionInfoResponse sessionInfo)
        {
            AtlasSessionWriter.UpdateSessionInfo(this.clientSession, sessionInfo);
        }

        public void EndSession()
        {
            // Wait for writing to session to end.
            do
            {
                Task.Delay(1000).Wait();
            } while (DateTime.Now - this.lastUpdated < TimeSpan.FromSeconds(120));

            this.sessionWriter.CloseSession(this.clientSession);
            this.connections.ForEach(x => this.streamApiClient.TryCloseConnection(x));
            this.configProcessor.ProcessPeriodicComplete -= this.OnProcessPeriodicComplete;
            this.configProcessor.ProcessEventComplete -= this.OnProcessorProcessEventComplete;
            this.configProcessor.ProcessRowComplete -= this.OnProcessRowComplete;
            this.SessionEnded = true;
        }

        public void StartSession(string sessionKey)
        {
            Console.WriteLine($"New Live SqlRaceSession found with key {sessionKey}.");
            var sessionInfo = this.streamApiClient.GetSessionInfo(sessionKey);
            var sqlSession = this.sessionWriter.CreateSession(
                sessionInfo.Identifier == "" ? "Untitled" : sessionInfo.Identifier,
                sessionInfo.Type);
            if (sqlSession == null)
            {
                Console.WriteLine("Failed to create session.");
                return;
            }

            this.SessionEnded = false;
            this.clientSession = sqlSession;
            this.sessionKey = sessionKey;

            this.configProcessor = new ConfigurationProcessor(this.sessionWriter, sqlSession);
            this.periodicDataHandler =
                new PeriodicDataHandler(this.sessionWriter, this.streamApiClient, sqlSession, this.configProcessor);
            this.rowDataHandler = new RowDataHandler(this.sessionWriter, this.streamApiClient, sqlSession, this.configProcessor);
            this.markerHandler = new MarkerHandler(this.sessionWriter, this.streamApiClient, sqlSession);
            this.eventDataHandler = new EventDataHandler(this.sessionWriter, this.streamApiClient, sqlSession, this.configProcessor);
            this.configProcessor.ProcessPeriodicComplete += this.OnProcessPeriodicComplete;
            this.configProcessor.ProcessEventComplete += this.OnProcessorProcessEventComplete;
            this.configProcessor.ProcessRowComplete += this.OnProcessRowComplete;
            Task.Run(this.UpdateSessionInfo);
        }

        private void UpdateSessionInfo()
        {
            bool isComplete;
            do
            {
                var sessionInfo = this.streamApiClient.GetSessionInfo(this.sessionKey);
                isComplete = sessionInfo.IsComplete;
                var newStreams = sessionInfo.Streams.Where(x => !this.streamsOffsetDictionary.ContainsKey(x)).ToList();
                if (newStreams.Any())
                {
                    var connectionDetails = new ConnectionDetails
                    {
                        DataSource = sessionInfo.DataSource,
                        EssentialsOffset = sessionInfo.EssentialsOffset,
                        MainOffset = sessionInfo.MainOffset,
                        Session = this.sessionKey,
                        StreamOffsets =
                        {
                            newStreams.Select(x =>
                                sessionInfo.TopicPartitionOffsets.GetValueOrDefault($"{sessionInfo.DataSource}.{x}:[0]",
                                    0)).ToList()
                        },
                        Streams = { newStreams }
                    };
                    var connection = this.streamApiClient.GetNewConnectionToSession(connectionDetails);
                    this.ReadPackets(this.cancellationTokenSource.Token, connection);
                    this.connections.Add(connection);
                }
                this.UpdateSessionInfo(sessionInfo);
                Task.Delay(TimeSpan.FromSeconds(1)).Wait();
            } while (!isComplete);
        }

        public void ReadPackets(CancellationToken cancellationToken, Connection? connectionDetails)
        {
            var streamReader = this.CreateStream(connectionDetails)?.ResponseStream;

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
                        foreach (var response in packetResponse.Response) await this.HandleNewPacket(response.Packet);
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
            this.lastUpdated = DateTime.Now;
            try
            {
                switch (packetType)
                {
                    case "PeriodicData":
                        {
                            var periodicDataPacket = PeriodicDataPacket.Parser.ParseFrom(content);
                            if (!this.periodicDataHandler.TryHandle(periodicDataPacket))
                            {
                                this.periodicDataQueue.Enqueue(periodicDataPacket);
                            }
                            break;
                        }
                    case "RowData":
                        {
                            var rowDataPacket = RowDataPacket.Parser.ParseFrom(content);
                            if (!this.rowDataHandler.TryHandle(rowDataPacket))
                            {
                                this.rowDataQueue.Enqueue(rowDataPacket);
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
                                this.eventPacketQueue.Enqueue(eventPacket);
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
            Connection? connectionDetails)
        {
            return this.streamApiClient.CreateReadPacketsStream(connectionDetails);
        }
        private void OnProcessorProcessEventComplete(object? sender, EventArgs e)
        {
            var dataQueue = this.eventPacketQueue.ToArray();
            this.eventPacketQueue.Clear();
            foreach (var packet in dataQueue) this.eventDataHandler.TryHandle(packet);
        }

        private void OnProcessRowComplete(object? sender, EventArgs e)
        {
            var dataQueue = this.rowDataQueue.ToArray();
            this.rowDataQueue.Clear();
            foreach (var packet in dataQueue)
                this.rowDataHandler.TryHandle(packet);
        }

        private void OnProcessPeriodicComplete(object? sender, EventArgs e)
        {
            var dataQueue = this.periodicDataQueue.ToArray();
            this.periodicDataQueue.Clear();
            foreach (var packet in dataQueue)
                this.periodicDataHandler.TryHandle(packet);
        }
    }
}