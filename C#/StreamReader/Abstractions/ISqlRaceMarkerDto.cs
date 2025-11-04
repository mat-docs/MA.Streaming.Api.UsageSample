// <copyright file="ISqlRaceMarkerDto.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

namespace Stream.Api.Stream.Reader.Abstractions
{
    public interface ISqlRaceMarkerDto
    {
        public string Name { get; }

        public long Timestamp { get; }
    }
}