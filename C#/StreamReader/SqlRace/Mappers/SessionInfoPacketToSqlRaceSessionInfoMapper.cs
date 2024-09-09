// <copyright file="SessionInfoPacketToSqlRaceSessionInfoMapper.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MA.Streaming.API;

using Stream.Api.Stream.Reader.Abstractions;
using Stream.Api.Stream.Reader.Dto;

namespace Stream.Api.Stream.Reader.SqlRace.Mappers
{
    public class SessionInfoPacketToSqlRaceSessionInfoMapper : BaseMapper
    {
        public static ISqlRaceDto MapSessionInfo(GetSessionInfoResponse packet)
        {
            return new SqlRaceSessionInfoDto
            {
                SessionIdentifier = packet.Identifier == "" ? "Untitled" : packet.Identifier
            };
        }
    }
}