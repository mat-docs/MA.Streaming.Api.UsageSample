// <copyright file="ISqlRaceRawCanDto.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MA.Streaming.OpenData;

namespace Stream.Api.Stream.Reader.Abstractions;

public interface ISqlRaceRawCanDto
{
    long Timestamp { get; }

    ushort CanBus { get; }

    uint CanId { get; }

    byte[] Payload { get; }

    byte CanType { get; }
}