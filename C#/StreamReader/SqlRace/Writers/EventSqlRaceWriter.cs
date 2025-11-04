// <copyright file="EventSqlRaceWriter.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

using MESL.SqlRace.Domain;

using Stream.Api.Stream.Reader.Abstractions;
using Stream.Api.Stream.Reader.Dto;

namespace Stream.Api.Stream.Reader.SqlRace.Writers
{
    internal class EventSqlRaceWriter : ConfigSensitiveSqlRaceWriter
    {
        public EventSqlRaceWriter(IClientSession clientSession, ReaderWriterLockSlim configLock)
            : base(clientSession, configLock)
        {
        }

        public override bool TryWrite(ISqlRaceDto data)
        {
            bool success;
            var eventDto = (SqlRaceEventDto)data;
            try
            {
                this.ConfigLock.EnterReadLock();
                this.ClientSession.Session.Events.AddEventData(eventDto.EventId, eventDto.GroupName, eventDto.Timestamp, eventDto.Data);
                success = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to write event due to {ex}.");
                success = false;
            }
            finally
            {
                this.ConfigLock.ExitReadLock();
            }

            return success;
        }
    }
}