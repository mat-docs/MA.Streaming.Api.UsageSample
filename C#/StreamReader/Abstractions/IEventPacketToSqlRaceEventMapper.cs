// <copyright file="IEventPacketToSqlRaceEventMapper.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MA.Streaming.OpenData;

namespace Stream.Api.Stream.Reader.Abstractions;

public interface IEventPacketToSqlRaceEventMapper
{
    SqlRaceEvent Map(EventPacket data);
}