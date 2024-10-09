// <copyright file="ISqlRaceSessionWriter.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

namespace Stream.Api.Stream.Reader.Abstractions
{
    internal interface ISqlRaceSessionWriter : ISqlRaceWriter
    {
        public long StartTimestamp { get; }

        public long EndTimestamp { get; }
    }
}