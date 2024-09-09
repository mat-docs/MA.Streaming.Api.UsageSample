// <copyright file="SqlRaceMarkerDto.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using Stream.Api.Stream.Reader.Abstractions;

namespace Stream.Api.Stream.Reader.Dto
{
    public class SqlRaceMarkerDto : ISqlRaceDto, ISqlRaceMarkerDto
    {
        public string DataType { get; } = "Marker";

        public string Name { get; set; }

        public long Timestamp { get; set; }
    }
}