// <copyright file="PeriodicSqlRaceWriter.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MESL.SqlRace.Domain;

using Stream.Api.Stream.Reader.Abstractions;
using Stream.Api.Stream.Reader.Dto;

namespace Stream.Api.Stream.Reader.SqlRace.Writers
{
    internal class PeriodicSqlRaceWriter : BaseSqlRaceWriter
    {
        public PeriodicSqlRaceWriter(IClientSession clientSession) : base(clientSession)
        {
        }

        public override bool TryWrite(ISqlRaceDto data)
        {
            var periodicDto = (SqlRacePeriodicDto)data;
            var channel = periodicDto.Channels[0];
            try
            {
                this.ClientSession.Session.AddChannelData(channel, periodicDto.Timestamp, periodicDto.Count, periodicDto.Data);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write periodic data due to {ex.Message}");
                return false;
            }

            return true;
        }
    }
}