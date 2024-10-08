﻿// <copyright file="LapSqlRaceWriter.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MESL.SqlRace.Domain;

using Stream.Api.Stream.Reader.Abstractions;

namespace Stream.Api.Stream.Reader.SqlRace.Writers
{
    internal class LapSqlRaceWriter : BaseSqlRaceWriter
    {
        public LapSqlRaceWriter(IClientSession clientSession) : base(clientSession)
        {
        }

        public override bool TryWrite(ISqlRaceDto data)
        {
            var lapDto = (ISqlRaceLapDto)data;
            var lap = new Lap(lapDto.Timestamp, lapDto.LapNumber, lapDto.TriggerSource, lapDto.Name, lapDto.CountForFastestLap);
            if (this.ClientSession.Session.LapCollection.Contains(lap))
            {
                // Drop the lap as its already in the session.
                return true;
            }

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