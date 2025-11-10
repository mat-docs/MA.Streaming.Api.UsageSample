// <copyright file="ISqlRaceLapDto.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

namespace Stream.Api.Stream.Reader.Abstractions
{
    public interface ISqlRaceLapDto
    {
        public string Name { get; }

        public short LapNumber { get; }

        public long Timestamp { get; }

        public bool CountForFastestLap { get; }

        public byte TriggerSource { get; }
    }
}