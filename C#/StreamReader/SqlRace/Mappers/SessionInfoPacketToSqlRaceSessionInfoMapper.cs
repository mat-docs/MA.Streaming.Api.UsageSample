// <copyright file="SessionInfoPacketToSqlRaceSessionInfoMapper.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

using MA.Streaming.API;

using Stream.Api.Stream.Reader.Abstractions;
using Stream.Api.Stream.Reader.Dto;

namespace Stream.Api.Stream.Reader.SqlRace.Mappers
{
    internal class SessionInfoPacketToSqlRaceSessionInfoMapper
    {
        public static ISqlRaceDto MapSessionInfo(GetSessionInfoResponse packet)
        {
            return new SqlRaceSessionInfoDto(packet.Identifier == "" ? "Untitled" : packet.Identifier);
        }
    }
}