// <copyright file="MarkerHandler.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MA.Streaming.OpenData;

using Stream.Api.Stream.Reader.Abstractions;
using Stream.Api.Stream.Reader.SqlRace.Mappers;

namespace Stream.Api.Stream.Reader.Handlers
{
    public class MarkerHandler(ISqlRaceWriter sessionWriter)
    {
        public bool TryHandle(MarkerPacket packet)
        {
            var mappedMarker = MarkerPacketToSqlRaceMarkerMapper.MapMarker(packet);
            return sessionWriter.TryWrite(mappedMarker);
        }
    }
}