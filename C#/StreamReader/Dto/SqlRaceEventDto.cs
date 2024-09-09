// <copyright file="SqlRaceEventDto.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using Stream.Api.Stream.Reader.Abstractions;

namespace Stream.Api.Stream.Reader.Dto
{
    public class SqlRaceEventDto : ISqlRaceEventDto, ISqlRaceDto
    {
        public string DataType { get; } = "Event";

        public int EventId { get; set; }

        public string GroupName { get; set; }

        public long Timestamp { get; set; }

        public IList<double> Data { get; set; }
    }
}