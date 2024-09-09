// <copyright file="EventPacketToSqlRaceEventMapper.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MA.Streaming.OpenData;

using Stream.Api.Stream.Reader.Abstractions;
using Stream.Api.Stream.Reader.Dto;

namespace Stream.Api.Stream.Reader.SqlRace.Mappers
{
    public class EventPacketToSqlRaceEventMapper : BaseMapper
    {
        public EventPacketToSqlRaceEventMapper(SessionConfig sessionConfig)
        {
            this.SessionConfig = sessionConfig;
        }

        public ISqlRaceDto MapEvent(EventPacket packet, string eventIdentifier)
        {
            var eventDef = this.SessionConfig.GetEventDefinition(eventIdentifier);
            return new SqlRaceEventDto
            {
                Data = new List<double>(packet.RawValues),
                EventId = eventDef.EventDefinitionId,
                GroupName = eventDef.GroupName,
                Timestamp = ConvertUnixToSqlRaceTime(packet.Timestamp)
            };
        }
    }
}