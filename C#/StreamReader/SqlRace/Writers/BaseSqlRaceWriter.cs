// <copyright file="BaseSqlRaceWriter.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MESL.SqlRace.Domain;

using Stream.Api.Stream.Reader.Abstractions;

namespace Stream.Api.Stream.Reader.SqlRace.Writers
{
    public abstract class BaseSqlRaceWriter : ISqlRaceWriter
    {
        protected IClientSession ClientSession { get; set; }

        public abstract bool TryWrite(ISqlRaceDto data);
    }
}