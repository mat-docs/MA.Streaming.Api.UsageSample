// <copyright file="RawCanDataHandler.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

using MA.Streaming.OpenData;

using Stream.Api.Stream.Reader.Abstractions;
using Stream.Api.Stream.Reader.SqlRace.Mappers;

namespace Stream.Api.Stream.Reader.Handlers
{
    internal class RawCanDataHandler : BaseHandler<RawCANDataPacket>
    {
        private readonly ISqlRaceWriter sessionWriter;

        public RawCanDataHandler(ISqlRaceWriter sessionWriter)
        {
            this.sessionWriter = sessionWriter;
        }

        public override void Handle(RawCANDataPacket packet)
        {
            this.Update();
            var dto = RawCanPacketToSqlRaceRawCanMapper.MapRawCan(packet);
            this.sessionWriter.TryWrite(dto);
        }
    }
}