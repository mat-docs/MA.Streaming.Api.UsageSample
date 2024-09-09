// <copyright file="EventPacketToSqlRaceEventMapper.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MA.Streaming.OpenData;

using Stream.Api.Stream.Reader.Abstractions;
using Stream.Api.Stream.Reader.Dto;

namespace Stream.Api.Stream.Reader.SqlRace.Mappers
{
    internal class EventPacketToSqlRaceEventMapper : BaseMapper
    {
        public EventPacketToSqlRaceEventMapper(SessionConfig sessionConfig)
            : base(sessionConfig)
        {
        }

        public ISqlRaceDto MapEvent(EventPacket packet, string eventIdentifier)
        {
            var eventDef = this.SessionConfig.GetEventDefinition(eventIdentifier);
            return new SqlRaceEventDto(
                eventDef.EventDefinitionId,
                eventDef.GroupName,
                ConvertUnixToSqlRaceTime(packet.Timestamp),
                new List<double>(packet.RawValues));
        }
    }
}