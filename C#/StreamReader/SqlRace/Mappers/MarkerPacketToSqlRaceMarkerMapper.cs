// <copyright file="MarkerPacketToSqlRaceMarkerMapper.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MA.Streaming.OpenData;

using Stream.Api.Stream.Reader.Abstractions;
using Stream.Api.Stream.Reader.Dto;

namespace Stream.Api.Stream.Reader.SqlRace.Mappers
{
    public class MarkerPacketToSqlRaceMarkerMapper : BaseMapper
    {
        public static ISqlRaceDto MapMarker(MarkerPacket packet)
        {
            if (packet.Type == "Lap Trigger")
            {
                return new SqlRaceLapDto
                {
                    CountForFastestLap = true,
                    LapNumber = (short)packet.Value,
                    Name = packet.Label,
                    Timestamp = ConvertUnixToSqlRaceTime(packet.Timestamp),
                    TriggerSource = BitConverter.GetBytes(0)[0]
                };
            }

            return new SqlRaceMarkerDto
            {
                Name = packet.Label,
                Timestamp = ConvertUnixToSqlRaceTime(packet.Timestamp)
            };
        }
    }
}