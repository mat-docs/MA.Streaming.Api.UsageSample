// <copyright file="MarkerHandler.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MA.Streaming.OpenData;

using Stream.Api.Stream.Reader.Abstractions;
using Stream.Api.Stream.Reader.SqlRace.Mappers;

namespace Stream.Api.Stream.Reader.Handlers
{
    internal class MarkerHandler(ISqlRaceWriter sessionWriter) : BaseHandler
    {
        public bool TryHandle(MarkerPacket packet)
        {
            this.Update();
            var mappedMarker = MarkerPacketToSqlRaceMarkerMapper.MapMarker(packet);
            return sessionWriter.TryWrite(mappedMarker);
        }
    }
}