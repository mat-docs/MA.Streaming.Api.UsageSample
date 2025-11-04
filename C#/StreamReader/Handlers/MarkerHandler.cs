// <copyright file="MarkerHandler.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

using MA.Streaming.OpenData;

using Stream.Api.Stream.Reader.Abstractions;
using Stream.Api.Stream.Reader.SqlRace.Mappers;

namespace Stream.Api.Stream.Reader.Handlers
{
    internal class MarkerHandler(ISqlRaceWriter sessionWriter) : BaseHandler<MarkerPacket>
    {
        public override void Handle(MarkerPacket packet)
        {
            this.Update();
            var mappedMarker = MarkerPacketToSqlRaceMarkerMapper.MapMarker(packet);
            sessionWriter.TryWrite(mappedMarker);
        }
    }
}