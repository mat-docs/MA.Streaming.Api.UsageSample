// <copyright file="ISqlRaceMarkerDto.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

namespace Stream.Api.Stream.Reader.Abstractions
{
    public interface ISqlRaceMarkerDto
    {
        public string Name { get; }

        public long Timestamp { get; }
    }
}