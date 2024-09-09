// <copyright file="SqlRaceSession.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MA.Streaming.API;

using MESL.SqlRace.Domain;

using Stream.Api.Stream.Reader.Abstractions;
using Stream.Api.Stream.Reader.SqlRace.Mappers;

using ISession = Stream.Api.Stream.Reader.Abstractions.ISession;

namespace Stream.Api.Stream.Reader.SqlRace
{
    internal class SqlRaceSession : ISession
    {
        private readonly StreamApiClient streamApiClient;
        private readonly List<IStreamApiReader> streamApiReaders;
        private readonly string sessionKey;
        private readonly Dictionary<string, long> streamsOffsetDictionary;
        private readonly ISqlRaceWriter sqlRaceWriter;
        private readonly IClientSession clientSession;
        private readonly IPacketHandler packetHandler;

        public SqlRaceSession(
            StreamApiClient streamApiClient,
            IClientSession clientSession,
            string sessionKey,
            ISqlRaceWriter sqlRaceWriter,
            IPacketHandler packetHandler)
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

                    foreach (var newStreamAndOffset in newStreams.Select(
                                 newStream => new Tuple<string, long>(
                                     newStream,
                                     sessionInfo.TopicPartitionOffsets.GetValueOrDefault(
                                         $"{sessionInfo.DataSource}.{newStream}:[0]",
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
                        Session = this.sessionKey,
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