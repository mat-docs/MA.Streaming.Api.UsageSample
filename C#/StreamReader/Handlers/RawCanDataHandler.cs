// <copyright file="RawCanDataHandler.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MA.Streaming.OpenData;

using Stream.Api.Stream.Reader.Abstractions;
using Stream.Api.Stream.Reader.SqlRace.Mappers;

namespace Stream.Api.Stream.Reader.Handlers
{
    internal class RawCanDataHandler : BaseHandler
    {
        private readonly ISqlRaceWriter sessionWriter;

        public RawCanDataHandler(ISqlRaceWriter sessionWriter)
        {
            this.sessionWriter = sessionWriter;
        }

        public bool TryHandle(RawCANDataPacket packet)
        {
            this.Update();
            var dto = RawCanPacketToSqlRaceRawCanMapper.MapRawCan(packet);
            return this.sessionWriter.TryWrite(dto);
        }
    }
}