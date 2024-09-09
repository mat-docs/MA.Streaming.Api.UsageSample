// <copyright file="ISqlRaceWriter.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

namespace Stream.Api.Stream.Reader.Abstractions
{
    public interface ISqlRaceWriter
    {
        public bool TryWrite(ISqlRaceDto data);
    }
}