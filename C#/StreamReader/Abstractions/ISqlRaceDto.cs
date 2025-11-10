// <copyright file="ISqlRaceDto.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

namespace Stream.Api.Stream.Reader.Abstractions
{
    public interface ISqlRaceDto
    {
        public string DataType { get; }

        public long Timestamp { get; }
    }
}