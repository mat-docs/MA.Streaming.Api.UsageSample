// <copyright file="ISqlRaceEventDto.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

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