// <copyright file="EventSqlRaceWriter.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MESL.SqlRace.Domain;

using Stream.Api.Stream.Reader.Abstractions;
using Stream.Api.Stream.Reader.Dto;

namespace Stream.Api.Stream.Reader.SqlRace.Writers
{
    public class EventSqlRaceWriter : BaseSqlRaceWriter
    {
        public EventSqlRaceWriter(IClientSession clientSession)
        {
            this.ClientSession = clientSession;
        }

        public override bool TryWrite(ISqlRaceDto data)
        {
            var eventDto = (SqlRaceEventDto)data;
            try
            {
                this.ClientSession.Session.Events.AddEventData(eventDto.EventId, eventDto.GroupName, eventDto.Timestamp, eventDto.Data);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to write event due to {ex}.");
                return false;
            }

            return true;
        }
    }
}