// <copyright file="SqlRacePeriodicDto.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

using Stream.Api.Stream.Reader.Abstractions;

namespace Stream.Api.Stream.Reader.Dto
{
    public class SqlRacePeriodicDto : ISqlRaceParameterDto, ISqlRaceDto
    {
        public SqlRacePeriodicDto(IReadOnlyList<uint> channels, long timestamp, byte[] data, uint interval, int count)
        {
            this.Channels = channels;
            this.Timestamp = timestamp;
            this.Data = data;
            this.Interval = interval;
            this.Count = count;
            this.DataType = "Periodic";
        }

        public string DataType { get; }

        public IReadOnlyList<uint> Channels { get; }

        public long Timestamp { get; }

        public byte[] Data { get; }

        public uint Interval { get; }

        public int Count { get; }
    }
}