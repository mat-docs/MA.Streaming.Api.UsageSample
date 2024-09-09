// <copyright file="LapSqlRaceWriter.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MESL.SqlRace.Domain;

using Stream.Api.Stream.Reader.Abstractions;

namespace Stream.Api.Stream.Reader.SqlRace.Writers
{
    public class LapSqlRaceWriter : BaseSqlRaceWriter
    {
        public LapSqlRaceWriter(IClientSession clientSession)
        {
            this.ClientSession = clientSession;
        }

        public override bool TryWrite(ISqlRaceDto data)
        {
            var lapDto = (ISqlRaceLapDto)data;
            var lap = new Lap(lapDto.Timestamp, lapDto.LapNumber, lapDto.TriggerSource, lapDto.Name, lapDto.CountForFastestLap);
            try
            {
                this.ClientSession.Session.LapCollection.Add(lap);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to write lap data due to {ex.Message}");
                return false;
            }

            return true;
        }
    }
}