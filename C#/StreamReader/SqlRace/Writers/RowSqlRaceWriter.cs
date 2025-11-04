// <copyright file="RowSqlRaceWriter.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

using MESL.SqlRace.Domain;

using Stream.Api.Stream.Reader.Abstractions;
using Stream.Api.Stream.Reader.Dto;

namespace Stream.Api.Stream.Reader.SqlRace.Writers
{
    internal class RowSqlRaceWriter : ConfigSensitiveSqlRaceWriter
    {
        public RowSqlRaceWriter(IClientSession clientSession, ReaderWriterLockSlim configLock)
            : base(clientSession, configLock)
        {
        }

        public override bool TryWrite(ISqlRaceDto data)
        {
            var rowDto = (SqlRaceRowDto)data;
            bool success;
            try
            {
                this.ConfigLock.EnterReadLock();
                this.ClientSession.Session.AddRowData(rowDto.Timestamp, new List<uint>(rowDto.Channels), rowDto.Data);
                success = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to write row data due to {ex.Message}");
                success = false;
            }
            finally
            {
                this.ConfigLock.ExitReadLock();
            }

            return success;
        }
    }
}