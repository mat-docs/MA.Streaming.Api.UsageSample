// <copyright file="ISqlRaceEventDto.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

namespace Stream.Api.Stream.Reader.Abstractions
{
    public interface ISqlRaceEventDto
    {
        int EventId { get; }

        string GroupName { get; }

        long Timestamp { get; }

        IList<double> Data { get; }
    }
}