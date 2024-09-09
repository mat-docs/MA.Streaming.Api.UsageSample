// <copyright file="SqlRaceWriter.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MESL.SqlRace.Domain;

using Stream.Api.Stream.Reader.Abstractions;
using Stream.Api.Stream.Reader.Dto;

namespace Stream.Api.Stream.Reader.SqlRace
{
    public class SqlRaceWriter : ISqlRaceWriter
    {
        private readonly object configLock;

        private readonly IClientSession clientSession;
        private readonly ISqlRaceWriter eventSqlRaceWriter;
        private readonly ISqlRaceWriter periodicSqlRaceWriter;
        private readonly ISqlRaceWriter rowSqlRaceWriter;
        private readonly ISqlRaceWriter markerSqlRaceWriter;
        private readonly ISqlRaceWriter lapSqlRaceWriter;
        private readonly ISqlRaceWriter sessionInfoWriter;

        public SqlRaceWriter(
            IClientSession clientSession,
            object configLock,
            ISqlRaceWriter eventSqlRaceWriter,
            ISqlRaceWriter periodicSqlRaceWriter,
            ISqlRaceWriter rowSqlRaceWriter,
            ISqlRaceWriter markerSqlRaceWriter,
            ISqlRaceWriter lapSqlRaceWriter,
            ISqlRaceWriter sessionInfoWriter)
        {
            this.configLock = configLock;
            this.clientSession = clientSession;
            this.eventSqlRaceWriter = eventSqlRaceWriter;
            this.periodicSqlRaceWriter = periodicSqlRaceWriter;
            this.rowSqlRaceWriter = rowSqlRaceWriter;
            this.markerSqlRaceWriter = markerSqlRaceWriter;
            this.lapSqlRaceWriter = lapSqlRaceWriter;
            this.sessionInfoWriter = sessionInfoWriter;
        }

        public bool TryWrite(ISqlRaceDto data)
        {
            var dataType = data.DataType;
            bool success;
            lock (this.configLock)
            {
                switch (data)
                {
                    case ISqlRaceEventDto:
                    {
                        success = this.eventSqlRaceWriter.TryWrite(data);
                        break;
                    }
                    case SqlRacePeriodicDto:
                    {
                        success = this.periodicSqlRaceWriter.TryWrite(data);
                        break;
                    }
                    case SqlRaceRowDto:
                    {
                        success = this.rowSqlRaceWriter.TryWrite(data);
                        break;
                    }
                    case SqlRaceMarkerDto:
                    {
                        success = this.markerSqlRaceWriter.TryWrite(data);
                        break;
                    }
                    case SqlRaceLapDto:
                    {
                        success = this.lapSqlRaceWriter.TryWrite(data);
                        break;
                    }
                    case SqlRaceSessionInfoDto:
                    {
                        success = this.sessionInfoWriter.TryWrite(data);
                        break;
                    }
                    default:
                    {
                        Console.WriteLine($"Unable to write SqlRace data for data type {dataType}.");
                        success = false;
                        break;
                    }
                }
            }

            return success;
        }
    }
}