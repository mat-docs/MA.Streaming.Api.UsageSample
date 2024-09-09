// <copyright file="RowSqlRaceWriter.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MESL.SqlRace.Domain;

using Stream.Api.Stream.Reader.Abstractions;
using Stream.Api.Stream.Reader.Dto;

namespace Stream.Api.Stream.Reader.SqlRace.Writers
{
    public class RowSqlRaceWriter : BaseSqlRaceWriter
    {
        public RowSqlRaceWriter(IClientSession clientSession)
        {
            this.ClientSession = clientSession;
        }

        public override bool TryWrite(ISqlRaceDto data)
        {
            var rowDto = (SqlRaceRowDto)data;
            try
            {
                this.ClientSession.Session.AddRowData(rowDto.Timestamp, new List<uint>(rowDto.Channels), rowDto.Data);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to write row data due to {ex.Message}");
                return false;
            }

            return true;
        }
    }
}