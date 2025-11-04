// <copyright file="ISqlRaceSessionWriter.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

namespace Stream.Api.Stream.Reader.Abstractions
{
    internal interface ISqlRaceSessionWriter : ISqlRaceWriter
    {
        public long StartTimestamp { get; }

        public long EndTimestamp { get; }
    }
}