// <copyright file="ISqlRaceRawCanDto.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

namespace Stream.Api.Stream.Reader.Abstractions;

public interface ISqlRaceRawCanDto
{
    long Timestamp { get; }

    ushort CanBus { get; }

    uint CanId { get; }

    byte[] Payload { get; }

    byte CanType { get; }
}