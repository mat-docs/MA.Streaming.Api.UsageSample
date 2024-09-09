// <copyright file="SessionInfoWriter.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MESL.SqlRace.Domain;

using Stream.Api.Stream.Reader.Abstractions;

namespace Stream.Api.Stream.Reader.SqlRace.Writers
{
    public class SessionInfoWriter : BaseSqlRaceWriter
    {
        public SessionInfoWriter(IClientSession clientSession)
        {
            this.ClientSession = clientSession;
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