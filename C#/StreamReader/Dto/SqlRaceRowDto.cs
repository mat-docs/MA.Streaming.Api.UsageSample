// <copyright file="SqlRaceRowDto.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

using Stream.Api.Stream.Reader.Abstractions;

namespace Stream.Api.Stream.Reader.Dto
{
    public class SqlRaceRowDto : ISqlRaceParameterDto, ISqlRaceDto
    {
        public SqlRaceRowDto(IReadOnlyList<uint> channels, long timestamp, byte[] data, uint interval)
        {
            this.Channels = channels;
            this.Timestamp = timestamp;
            this.Data = data;
            this.Interval = interval;
            this.DataType = "Row";
        }

        public string DataType { get; }

        public IReadOnlyList<uint> Channels { get; }

        public long Timestamp { get; }

        public byte[] Data { get; }

        public uint Interval { get; }
    }
}