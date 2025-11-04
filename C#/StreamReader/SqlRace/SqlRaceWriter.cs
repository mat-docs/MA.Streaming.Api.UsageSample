// <copyright file="SqlRaceWriter.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

using Stream.Api.Stream.Reader.Abstractions;
using Stream.Api.Stream.Reader.Dto;

namespace Stream.Api.Stream.Reader.SqlRace
{
    public class SqlRaceWriter : ISqlRaceSessionWriter
    {
        private readonly ISqlRaceWriter eventSqlRaceWriter;
        private readonly ISqlRaceWriter periodicSqlRaceWriter;
        private readonly ISqlRaceWriter rowSqlRaceWriter;
        private readonly ISqlRaceWriter markerSqlRaceWriter;
        private readonly ISqlRaceWriter lapSqlRaceWriter;
        private readonly ISqlRaceWriter sessionInfoWriter;
        private readonly ISqlRaceWriter errorSqlRaceWriter;
        private readonly ISqlRaceWriter rawCanSqlRaceWriter;
        private readonly ISqlRaceWriter synchroSqlRaceWriter;
        private readonly ISqlRaceWriter coverageCursorWriter;

        public SqlRaceWriter(
            ISqlRaceWriter eventSqlRaceWriter,
            ISqlRaceWriter periodicSqlRaceWriter,
            ISqlRaceWriter rowSqlRaceWriter,
            ISqlRaceWriter markerSqlRaceWriter,
            ISqlRaceWriter lapSqlRaceWriter,
            ISqlRaceWriter sessionInfoWriter,
            ISqlRaceWriter errorSqlRaceWriter,
            ISqlRaceWriter rawCanSqlRaceWriter,
            ISqlRaceWriter synchroSqlRaceWriter,
            ISqlRaceWriter coverageCursorWriter)
        {
            this.eventSqlRaceWriter = eventSqlRaceWriter;
            this.periodicSqlRaceWriter = periodicSqlRaceWriter;
            this.rowSqlRaceWriter = rowSqlRaceWriter;
            this.markerSqlRaceWriter = markerSqlRaceWriter;
            this.lapSqlRaceWriter = lapSqlRaceWriter;
            this.sessionInfoWriter = sessionInfoWriter;
            this.errorSqlRaceWriter = errorSqlRaceWriter;
            this.rawCanSqlRaceWriter = rawCanSqlRaceWriter;
            this.synchroSqlRaceWriter = synchroSqlRaceWriter;
            this.coverageCursorWriter = coverageCursorWriter;
        }

        public long StartTimestamp { get; private set; } = long.MaxValue;

        public long EndTimestamp { get; private set; } = long.MinValue;

        public bool TryWrite(ISqlRaceDto data)
        {
            var dataType = data.DataType;
            bool success;
            if (dataType != "Session Info" &&
                dataType != "Lap" &&
                dataType != "Marker" &&
                dataType != "Event" &&
                dataType != "Error")
            {
                if (data.Timestamp < this.StartTimestamp)
                {
                    this.StartTimestamp = data.Timestamp;
                }

                if (data.Timestamp > this.EndTimestamp)
                {
                    this.EndTimestamp = data.Timestamp;
                }
            }

            switch (data)
            {
                case SqlRaceEventDto:
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
                case SqlRaceErrorDto:
                {
                    success = this.errorSqlRaceWriter.TryWrite(data);
                    break;
                }
                case SqlRaceRawCanDto:
                {
                    success = this.rawCanSqlRaceWriter.TryWrite(data);
                    break;
                }
                case SqlRaceSynchroDto:
                {
                    success = this.synchroSqlRaceWriter.TryWrite(data);
                    break;
                }
                case SqlRaceCoverageCursor:
                {
                    success = this.coverageCursorWriter.TryWrite(data);
                    break;
                }
                default:
                {
                    Console.WriteLine($"Unable to write SqlRace data for data type {dataType}.");
                    success = false;
                    break;
                }
            }

            if (!success)
            {
                Console.WriteLine($"Unable to write data for {dataType}.");
            }

            return success;
        }
    }
}