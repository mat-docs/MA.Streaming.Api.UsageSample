// <copyright file="SessionInfoWriter.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

using MESL.SqlRace.Domain;

using Stream.Api.Stream.Reader.Abstractions;

namespace Stream.Api.Stream.Reader.SqlRace.Writers
{
    internal class SessionInfoWriter : BaseSqlRaceWriter
    {
        public SessionInfoWriter(IClientSession clientSession)
            : base(clientSession)
        {
        }

        public override bool TryWrite(ISqlRaceDto data)
        {
            var sessionInfoDto = (ISqlRaceSessionInfoDto)data;
            try
            {
                this.ClientSession.Session.UpdateIdentifier(sessionInfoDto.SessionIdentifier);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to update session info due to {ex.Message}");
                return false;
            }

            return true;
        }
    }
}