// <copyright file="SqlRaceRowDto.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using Stream.Api.Stream.Reader.Abstractions;

namespace Stream.Api.Stream.Reader.Dto
{
    public class SqlRaceRowDto : ISqlRaceParameterDto, ISqlRaceDto
    {
        public string DataType { get; } = "Row";

        public IReadOnlyList<uint> Channels { get; set; }

        public long Timestamp { get; set; }

        public byte[] Data { get; set; }

        public uint Interval { get; set; }
    }
}