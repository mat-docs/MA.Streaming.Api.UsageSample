// <copyright file="SqlRaceMarkerDto.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using Stream.Api.Stream.Reader.Abstractions;

namespace Stream.Api.Stream.Reader.Dto
{
    public class SqlRaceMarkerDto : ISqlRaceDto, ISqlRaceMarkerDto
    {
        public SqlRaceMarkerDto(string name, long timestamp)
        {
            this.Name = name;
            this.Timestamp = timestamp;
            this.DataType = "Marker";
        }

        public string DataType { get; }

        public string Name { get; }

        public long Timestamp { get; }
    }
}