// <copyright file="SqlRaceEventDto.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using Stream.Api.Stream.Reader.Abstractions;

namespace Stream.Api.Stream.Reader.Dto
{
    public class SqlRaceEventDto : ISqlRaceEventDto, ISqlRaceDto
    {
        public SqlRaceEventDto(int eventId, string groupName, long timestamp, IList<double> data)
        {
            this.EventId = eventId;
            this.GroupName = groupName;
            this.Timestamp = timestamp;
            this.Data = data;
            this.DataType = "Event";
        }

        public string DataType { get; }

        public int EventId { get; }

        public string GroupName { get; }

        public long Timestamp { get; }

        public IList<double> Data { get; }
    }
}