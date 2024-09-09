// <copyright file="SqlRaceLapDto.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using Stream.Api.Stream.Reader.Abstractions;

namespace Stream.Api.Stream.Reader.Dto
{
    public class SqlRaceLapDto : ISqlRaceDto, ISqlRaceLapDto
    {
        public SqlRaceLapDto(string name, short lapNumber, long timestamp, bool countForFastestLap, byte triggerSource)
        {
            this.Name = name;
            this.LapNumber = lapNumber;
            this.Timestamp = timestamp;
            this.CountForFastestLap = countForFastestLap;
            this.TriggerSource = triggerSource;
            this.DataType = "Lap";
        }

        public string DataType { get; }

        public string Name { get; }

        public short LapNumber { get; }

        public long Timestamp { get; }

        public bool CountForFastestLap { get; }

        public byte TriggerSource { get; }
    }
}