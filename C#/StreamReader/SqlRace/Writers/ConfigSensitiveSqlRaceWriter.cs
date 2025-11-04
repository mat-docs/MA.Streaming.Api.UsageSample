// <copyright file="ConfigSensitiveSqlRaceWriter.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

using MESL.SqlRace.Domain;

namespace Stream.Api.Stream.Reader.SqlRace.Writers
{
    internal abstract class ConfigSensitiveSqlRaceWriter : BaseSqlRaceWriter
    {
        protected ConfigSensitiveSqlRaceWriter(IClientSession clientSession, ReaderWriterLockSlim configLock)
            : base(clientSession)
        {
            this.ConfigLock = configLock;
        }

        protected ReaderWriterLockSlim ConfigLock { get; }
    }
}