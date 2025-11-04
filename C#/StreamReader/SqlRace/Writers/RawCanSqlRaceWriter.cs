// <copyright file="RawCanSqlRaceWriter.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

using MESL.SqlRace.Domain;

using Stream.Api.Stream.Reader.Abstractions;

namespace Stream.Api.Stream.Reader.SqlRace.Writers
{
    internal class RawCanSqlRaceWriter : BaseSqlRaceWriter
    {
        public RawCanSqlRaceWriter(IClientSession clientSession)
            : base(clientSession)
        {
        }

        public override bool TryWrite(ISqlRaceDto data)
        {
            if (data is not ISqlRaceRawCanDto rawCanDto)
            {
                return false;
            }

            try
            {
                this.ClientSession.Session.CanData.AddCanData(rawCanDto.Timestamp, rawCanDto.CanType, rawCanDto.CanBus, rawCanDto.CanId, rawCanDto.Payload);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to write raw can due to {ex.Message}");
                Console.WriteLine($"The size of the error message is {rawCanDto.Payload.Length} bytes.");
                return false;
            }
        }
    }
}