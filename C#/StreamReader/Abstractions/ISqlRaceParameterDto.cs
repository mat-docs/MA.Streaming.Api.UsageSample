// <copyright file="ISqlRaceParameterDto.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

namespace Stream.Api.Stream.Reader.Abstractions
{
    public interface ISqlRaceParameterDto
    {
        long Timestamp { get; }

        IReadOnlyList<uint> Channels { get; }

        byte[] Data { get; }

        uint Interval { get; }
    }
}