using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MESL.SqlRace.Domain;

using Microsoft.CodeAnalysis.CSharp.Syntax;

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
