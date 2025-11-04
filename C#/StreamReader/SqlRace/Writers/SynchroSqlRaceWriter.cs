// <copyright file="SynchroSqlRaceWriter.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

using MESL.SqlRace.Domain;

using Stream.Api.Stream.Reader.Abstractions;

namespace Stream.Api.Stream.Reader.SqlRace.Writers
{
    internal class SynchroSqlRaceWriter : ConfigSensitiveSqlRaceWriter
    {
        public SynchroSqlRaceWriter(IClientSession clientSession, ReaderWriterLockSlim configLock)
            : base(clientSession, configLock)
        {
        }

        public override bool TryWrite(ISqlRaceDto data)
        {
            if (data is not ISqlRaceSynchroDto synchroDto)
            {
                return false;
            }

            try
            {
                this.ConfigLock.EnterReadLock();
                this.ClientSession.Session.AddSynchroChannelData(
                    synchroDto.Timestamp,
                    synchroDto.ChannelId,
                    synchroDto.NumberOfSamples,
                    synchroDto.DeltaScale,
                    synchroDto.Data);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to add synchro data due to {ex.Message}");
                return false;
            }
            finally
            {
                this.ConfigLock.ExitReadLock();
            }

            return true;
        }
    }
}