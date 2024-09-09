// <copyright file="SqlRaceSessionFactory.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MA.Streaming.API;

using MAT.OCS.Core;

using MESL.SqlRace.Domain;

using Stream.Api.Stream.Reader.Handlers;
using Stream.Api.Stream.Reader.SqlRace.Mappers;
using Stream.Api.Stream.Reader.SqlRace.SqlRaceConfigProcessor;
using Stream.Api.Stream.Reader.SqlRace.Writers;

using ISession = Stream.Api.Stream.Reader.Abstractions.ISession;

namespace Stream.Api.Stream.Reader.SqlRace
{
    internal static class SqlRaceSessionFactory
    {
        public static ISession CreateSession(GetSessionInfoResponse sessionInfo, StreamApiClient streamApiClient, string connectionString, string sessionKey)
        {
            var sessionName = sessionInfo.Identifier == "" ? "Untitled" : sessionInfo.Identifier;

            var sessionManager = SessionManager.CreateSessionManager();
            var configManager = new ConfigurationSetManager();
            var defaultConversion = RationalConversion.CreateSimple1To1Conversion("DefaultConversion", "", "%5.2f");
            var sqlSessionKey = SessionKey.NewKey();
            var sessionDate = DateTime.Now;
            var clientSession = sessionManager.CreateSession(
                connectionString,
                sqlSessionKey,
                sessionName,
                sessionDate,
                sessionInfo.Type);
            var configLock = new object();
            var sessionConfig = new SessionConfig();
            var sessionWriter = new SqlRaceWriter(
                configLock,
                new EventSqlRaceWriter(clientSession),
                new PeriodicSqlRaceWriter(clientSession),
                new RowSqlRaceWriter(clientSession),
                new MarkerSqlRaceWriter(clientSession),
                new LapSqlRaceWriter(clientSession),
                new SessionInfoWriter(clientSession));
            var packetHandler = new PacketHandler(
                new PeriodicDataHandler(
                    sessionWriter,
                    streamApiClient,
                    sessionConfig,
                    new PeriodicConfigProcessor(configManager, defaultConversion, clientSession, configLock, sessionConfig),
                    new PeriodicPacketToSqlRaceParameterMapper(sessionConfig)),
                new RowDataHandler(
                    sessionWriter,
                    streamApiClient,
                    sessionConfig,
                    new RowConfigProcessor(configManager, defaultConversion, clientSession, configLock, sessionConfig),
                    new RowPacketToSqlRaceParameterMapper(sessionConfig)),
                new MarkerHandler(sessionWriter),
                new EventDataHandler(
                    sessionWriter,
                    streamApiClient,
                    sessionConfig,
                    new EventConfigProcessor(configManager, defaultConversion, clientSession, configLock, sessionConfig),
                    new EventPacketToSqlRaceEventMapper(sessionConfig)));

            Console.WriteLine($"New SqlRaceSession is created with name {sessionName}.");
            return new SqlRaceSession(streamApiClient, clientSession, sessionKey, sessionWriter, packetHandler);
        }
    }
}