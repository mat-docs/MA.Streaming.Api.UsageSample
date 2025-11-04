// <copyright file="ISqlRaceSynchroDto.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

namespace Stream.Api.Stream.Reader.Abstractions
{
    public interface ISqlRaceSynchroDto
    {
        uint ChannelId { get; }

        long Timestamp { get; }

        byte NumberOfSamples { get; }

        uint DeltaScale { get; }

        byte[] Data { get; }
    }
}