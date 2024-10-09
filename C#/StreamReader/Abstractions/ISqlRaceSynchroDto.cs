// <copyright file="ISqlRaceSynchroDto.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

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