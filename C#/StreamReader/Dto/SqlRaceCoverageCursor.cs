// <copyright file="SqlRaceCoverageCursor.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

using Stream.Api.Stream.Reader.Abstractions;

namespace Stream.Api.Stream.Reader.Dto;

internal class SqlRaceCoverageCursor : ISqlRaceDto
{
    public SqlRaceCoverageCursor(long timestamp)
    {
        this.Timestamp = timestamp;
        this.DataType = "CoverageCursor";
    }

    public string DataType { get; }

    public long Timestamp { get; }
}