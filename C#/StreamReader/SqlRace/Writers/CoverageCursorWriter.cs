// <copyright file="CoverageCursorWriter.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

using MESL.SqlRace.Domain;

using Stream.Api.Stream.Reader.Abstractions;
using Stream.Api.Stream.Reader.Dto;

namespace Stream.Api.Stream.Reader.SqlRace.Writers;

internal class CoverageCursorWriter : BaseSqlRaceWriter
{
    public CoverageCursorWriter(IClientSession clientSession)
        : base(clientSession)
    {
    }

    public override bool TryWrite(ISqlRaceDto data)
    {
        if (data is not SqlRaceCoverageCursor coverageCursorData)
        {
            return false;
        }

        this.ClientSession.Session.SetCoverageCursor(coverageCursorData.Timestamp);
        return true;
    }
}