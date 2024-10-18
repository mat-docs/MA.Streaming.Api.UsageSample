// <copyright file="RawCanPacketToSqlRaceRawCanMapper.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MA.Streaming.OpenData;

using Stream.Api.Stream.Reader.Abstractions;
using Stream.Api.Stream.Reader.Dto;

namespace Stream.Api.Stream.Reader.SqlRace.Mappers
{
    internal class RawCanPacketToSqlRaceRawCanMapper
    {
        public static ISqlRaceDto MapRawCan(RawCANDataPacket packet)
        {
            // In SQL Race 0 is CanType Transmit and 1 is CanType Receive.
            var canTypeBytes = byte.Parse(packet.Type == CanType.Transmit ? "0" : "1");
            if (packet.Payload.IsEmpty)
            {
                Console.WriteLine($"Packet with empty payload was sent.");
            }
            return new SqlRaceRawCanDto(packet.Timestamp.ToSqlRaceTime(), (ushort)packet.Bus, packet.CanId, packet.Payload.ToByteArray(), canTypeBytes);
        }
    }
}