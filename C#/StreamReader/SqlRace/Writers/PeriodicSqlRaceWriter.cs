// <copyright file="PeriodicSqlRaceWriter.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

using MESL.SqlRace.Domain;

using Stream.Api.Stream.Reader.Abstractions;
using Stream.Api.Stream.Reader.Dto;

namespace Stream.Api.Stream.Reader.SqlRace.Writers
{
    internal class PeriodicSqlRaceWriter : ConfigSensitiveSqlRaceWriter
    {
        public PeriodicSqlRaceWriter(IClientSession clientSession, ReaderWriterLockSlim configLock)
            : base(clientSession, configLock)
        {
        }

        public override bool TryWrite(ISqlRaceDto data)
        {
            var periodicDto = (SqlRacePeriodicDto)data;
            var channel = periodicDto.Channels[0];
            bool success;
            try
            {
                this.ConfigLock.EnterReadLock();
                this.ClientSession.Session.AddChannelData(channel, periodicDto.Timestamp, periodicDto.Count, periodicDto.Data);
                success = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write periodic data due to {ex.Message}");
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