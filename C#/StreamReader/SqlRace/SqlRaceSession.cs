// <copyright file="SqlRaceSession.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

using MA.Streaming.API;
using MA.Streaming.OpenData;

using MESL.SqlRace.Common.Extensions;
using MESL.SqlRace.Domain;

using Stream.Api.Stream.Reader.Abstractions;
using Stream.Api.Stream.Reader.SqlRace.Mappers;

using ISession = Stream.Api.Stream.Reader.Abstractions.ISession;

namespace Stream.Api.Stream.Reader.SqlRace
{
    internal class SqlRaceSession : ISession
    {
        private const byte TriggerSourceStart = 0;
        private const string DefaultLapName = "Out Lap";
        private readonly StreamApiClient streamApiClient;
        private readonly List<IStreamApiReader> streamApiReaders;
        private readonly string sessionKey;
        private readonly Dictionary<string, long> streamsOffsetDictionary;
        private readonly ISqlRaceSessionWriter sqlRaceWriter;
        private readonly IClientSession clientSession;
        private readonly IPacketHandler<Packet> packetHandler;

        public SqlRaceSession(
            StreamApiClient streamApiClient,
            IClientSession clientSession,
            string sessionKey,
            ISqlRaceSessionWriter sqlRaceWriter,
            IPacketHandler<Packet> packetHandler)
        {
            this.streamApiClient = streamApiClient;
            this.streamApiReaders = [];
            this.sessionKey = sessionKey;
            this.streamsOffsetDictionary = [];
            this.SessionEnded = false;
            this.packetHandler = packetHandler;
            this.sqlRaceWriter = sqlRaceWriter;
            this.clientSession = clientSession;
        }

        public bool SessionEnded { get; private set; }

        public void EndSession()
        {
            var identifier = this.clientSession.Session.Identifier;
            this.streamApiReaders.ForEach(x => x.Stop());

            if (this.clientSession.Session.LapCollection.Count == 0)
            {
                var lap = new Lap(this.clientSession.Session.StartTime, 1, TriggerSourceStart, DefaultLapName, false);
                this.clientSession.Session.LapCollection.Add(lap);
            }

            this.packetHandler.Stop();
            Console.WriteLine(
                $"Setting start and end time to {this.sqlRaceWriter.StartTimestamp.ToDateTime().TimeOfDay} and {this.sqlRaceWriter.EndTimestamp.ToDateTime().TimeOfDay}");
            this.clientSession.Session.SetStartTime(this.sqlRaceWriter.StartTimestamp);
            this.clientSession.Session.SetEndTime(this.sqlRaceWriter.EndTimestamp);
            this.clientSession.Session.SetSessionTimerange(this.sqlRaceWriter.StartTimestamp, this.sqlRaceWriter.EndTimestamp);
            this.clientSession.Session.Flush();
            this.clientSession.Session.EndData();
            this.clientSession.Close();
            this.SessionEnded = true;
            Console.WriteLine($"Closed SqlRaceSession {identifier}.");
        }

        public void StartSession()
        {
            Task.Run(this.UpdateSessionInfo);
        }

        private void UpdateSessionInfo()
        {
            bool isComplete;
            do
            {
                var sessionInfo = this.streamApiClient.GetSessionInfo(this.sessionKey);

                if (sessionInfo is null)
                {
                    Console.WriteLine("Unable to get session info.");
                    return;
                }

                isComplete = sessionInfo.IsComplete;
                var newStreams = sessionInfo.Streams.Where(x => !this.streamsOffsetDictionary.ContainsKey(x)).ToList();
                if (newStreams.Any())
                {
                    var streamList = new List<Tuple<string, long>>();

                    foreach (var newStreamAndOffset in newStreams.Select(newStream => new Tuple<string, long>(
                                 newStream,
                                 sessionInfo.TopicPartitionOffsets.GetValueOrDefault(
                                     $"{sessionInfo.DataSource}.{newStream}:0",
                                     0))))
                    {
                        streamList.Add(newStreamAndOffset);
                        this.streamsOffsetDictionary.Add(newStreamAndOffset.Item1, newStreamAndOffset.Item2);
                    }

                    var connectionDetails = new ConnectionDetails
                    {
                        DataSource = sessionInfo.DataSource,
                        EssentialsOffset = sessionInfo.EssentialsOffset,
                        MainOffset = sessionInfo.MainOffset,
                        SessionKey = this.sessionKey,
                        StreamOffsets =
                        {
                            streamList.Select(x => x.Item2).ToList()
                        },
                        Streams =
                        {
                            streamList.Select(x => x.Item1).ToList()
                        }
                    };
                    var connection = this.streamApiClient.GetNewConnectionToSession(connectionDetails);
                    if (connection == null)
                    {
                        Console.WriteLine("Connection is null");
                        return;
                    }

                    var streamApiReader = new StreamApiReader.StreamApiReader(connection, this.packetHandler, this.streamApiClient);
                    this.streamApiReaders.Add(streamApiReader);
                    streamApiReader.Start();
                }

                var sessionData = SessionInfoPacketToSqlRaceSessionInfoMapper.MapSessionInfo(sessionInfo);
                this.sqlRaceWriter.TryWrite(sessionData);
                Task.Delay(TimeSpan.FromSeconds(1)).Wait();
            }
            while (!isComplete);
        }
    }
}