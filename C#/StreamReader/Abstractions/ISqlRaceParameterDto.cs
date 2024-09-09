// <copyright file="ISqlRaceParameterDto.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

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