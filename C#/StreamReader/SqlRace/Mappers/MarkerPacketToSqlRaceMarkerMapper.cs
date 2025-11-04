// <copyright file="MarkerPacketToSqlRaceMarkerMapper.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

using MA.Streaming.OpenData;

using Stream.Api.Stream.Reader.Abstractions;
using Stream.Api.Stream.Reader.Dto;

namespace Stream.Api.Stream.Reader.SqlRace.Mappers
{
    internal class MarkerPacketToSqlRaceMarkerMapper
    {
        public static ISqlRaceDto MapMarker(MarkerPacket packet)
        {
            if (packet.Type == "Lap Trigger")
            {
                return new SqlRaceLapDto(
                    packet.Label,
                    (short)packet.Value,
                    packet.Timestamp.ToSqlRaceTime(),
                    true,
                    byte.Parse(packet.Source));
            }

            return new SqlRaceMarkerDto(packet.Label, packet.Timestamp.ToSqlRaceTime());
        }
    }
}