// <copyright file="CoverageCursorHandler.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MA.Streaming.OpenData;

using Stream.Api.Stream.Reader.Abstractions;
using Stream.Api.Stream.Reader.Dto;
using Stream.Api.Stream.Reader.SqlRace.Mappers;

namespace Stream.Api.Stream.Reader.Handlers;

internal class CoverageCursorHandler : BaseHandler<CoverageCursorInfoPacket>
{
    private readonly ISqlRaceWriter sqlRaceWriter;

    public CoverageCursorHandler(ISqlRaceWriter sqlRaceWriter)
    {
        this.sqlRaceWriter = sqlRaceWriter;
    }

    public override void Handle(CoverageCursorInfoPacket packet)
    {
        this.sqlRaceWriter.TryWrite(new SqlRaceCoverageCursor(packet.CoverageCursorTime.ToSqlRaceTime()));
    }
}