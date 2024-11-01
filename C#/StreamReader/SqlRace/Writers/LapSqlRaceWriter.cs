// <copyright file="LapSqlRaceWriter.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MESL.SqlRace.Common.Extensions;
using MESL.SqlRace.Domain;

using Stream.Api.Stream.Reader.Abstractions;

namespace Stream.Api.Stream.Reader.SqlRace.Writers
{
    internal class LapSqlRaceWriter : BaseSqlRaceWriter
    {
        private const string OutLap = "Out Lap";
        private const string InLap = "In Lap";
        private const string PitLane = "Pit Lane";
        private const byte MainTriggerSource = 0;
        private const byte PitLaneTriggerSource = 1;
        private Lap? previousLap;

        public LapSqlRaceWriter(IClientSession clientSession)
            : base(clientSession)
        {
        }

        public override bool TryWrite(ISqlRaceDto data)
        {
            var lapDto = (ISqlRaceLapDto)data;
            var minimumGuardTime = 10.SecondsToNanoseconds();
            Lap newLap;

            if (this.previousLap is not null &&
                lapDto.Timestamp - this.previousLap.StartTime < minimumGuardTime)
            {
                Console.WriteLine($"Rejecting lap marker {lapDto.Name} at {lapDto.Timestamp} since its less than the minimum guard time.");
                return true;
            }

            // First lap is always Out Lap.
            if (this.previousLap is null)
            {
                newLap = new Lap(lapDto.Timestamp, lapDto.LapNumber, lapDto.TriggerSource, OutLap, false);
            }
            // Pit Lane
            else if (lapDto.TriggerSource == PitLaneTriggerSource)
            {
                newLap = new Lap(lapDto.Timestamp, lapDto.LapNumber, lapDto.TriggerSource, PitLane, false);
                // If the previous trigger source is main, then set it to In Lap.
                if (this.previousLap.TriggerSource == MainTriggerSource)
                {
                    this.previousLap.Name = InLap;
                    this.previousLap.CountForFastestLap = false;
                }
            }
            // Out Lap after coming out of the pits.
            else if (this.previousLap.Name == PitLane &&
                     lapDto.TriggerSource == MainTriggerSource)
            {
                this.previousLap.Name = OutLap;
                this.previousLap.CountForFastestLap = false;
                newLap = new Lap(lapDto.Timestamp, lapDto.LapNumber, lapDto.TriggerSource, OutLap, false);
            }
            // Normal Lap
            else
            {
                newLap = new Lap(lapDto.Timestamp, lapDto.LapNumber, lapDto.TriggerSource, lapDto.Name, false);
            }

            if (this.ClientSession.Session.LapCollection.Contains(newLap))
            {
                Console.WriteLine($"Rejecting lap marker {lapDto.Name} at {lapDto.Timestamp} since its already in the session.");
                return true;
            }

            if (newLap.StartTime < this.ClientSession.Session.StartTime && this.previousLap is not null)
            {
                Console.WriteLine($"Rejecting lap marker {lapDto.Name} at {lapDto.Timestamp} since its earlier than the start time.");
                return true;
            }

            try
            {
                this.ClientSession.Session.LapCollection.Add(newLap);

                if (this.previousLap is null)
                {
                    this.previousLap = newLap;
                }
                else
                {
                    if (this.previousLap.Name != OutLap &&
                        this.previousLap.Name != InLap &&
                        this.previousLap.Name != PitLane)
                    {
                        this.previousLap.CountForFastestLap = true;
                    }
                    this.ClientSession.Session.LapCollection.Update(this.previousLap);
                    this.previousLap = newLap;
                }
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