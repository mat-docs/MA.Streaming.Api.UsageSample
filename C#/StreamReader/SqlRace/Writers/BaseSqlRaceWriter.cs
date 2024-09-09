// <copyright file="BaseSqlRaceWriter.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MESL.SqlRace.Domain;

using Stream.Api.Stream.Reader.Abstractions;

namespace Stream.Api.Stream.Reader.SqlRace.Writers
{
    internal abstract class BaseSqlRaceWriter : ISqlRaceWriter
    {
        protected BaseSqlRaceWriter(IClientSession clientSession)
        {
            this.ClientSession = clientSession;
        }

        protected IClientSession ClientSession { get; }

        public abstract bool TryWrite(ISqlRaceDto data);
    }
}