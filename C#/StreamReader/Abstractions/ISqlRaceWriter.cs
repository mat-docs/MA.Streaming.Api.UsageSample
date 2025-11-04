// <copyright file="ISqlRaceWriter.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

namespace Stream.Api.Stream.Reader.Abstractions
{
    public interface ISqlRaceWriter
    {
        public bool TryWrite(ISqlRaceDto data);
    }
}