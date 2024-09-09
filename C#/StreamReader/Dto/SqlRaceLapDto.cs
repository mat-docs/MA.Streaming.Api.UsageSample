// <copyright file="SqlRaceLapDto.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using Stream.Api.Stream.Reader.Abstractions;

namespace Stream.Api.Stream.Reader.Dto
{
    public class SqlRaceLapDto : ISqlRaceDto, ISqlRaceLapDto
    {
        public string DataType { get; } = "Lap";

        public string Name { get; set; }

        public short LapNumber { get; set; }

        public long Timestamp { get; set; }

        public bool CountForFastestLap { get; set; }

        public byte TriggerSource { get; set; }
    }
}