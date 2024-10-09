// <copyright file="ErrorPacketToSqlRaceErrorMapper.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MA.Streaming.OpenData;

using Stream.Api.Stream.Reader.Abstractions;
using Stream.Api.Stream.Reader.Dto;

namespace Stream.Api.Stream.Reader.SqlRace.Mappers
{
    internal class ErrorPacketToSqlRaceErrorMapper : BaseMapper
    {
        public ErrorPacketToSqlRaceErrorMapper(SessionConfig sessionConfig)
            : base(sessionConfig)
        {
        }

        public ISqlRaceDto MapError(ErrorPacket packet)
        {
            var errorDef = this.SessionConfig.GetErrorDefinition(packet.Name);
            var currentChannel = this.SessionConfig.GetParameterChannelId(errorDef.CurrentErrorIdentifier);
            var loggedChannel = this.SessionConfig.GetParameterChannelId(errorDef.LoggedErrorIdentifier);
            return new SqlRaceErrorDto(
                packet.ErrorIdentifier,
                errorDef.CurrentErrorIdentifier,
                currentChannel,
                errorDef.LoggedErrorIdentifier,
                loggedChannel,
                packet.Timestamp.ToSqlRaceTime(),
                packet.Type,
                packet.Status);
        }
    }
}