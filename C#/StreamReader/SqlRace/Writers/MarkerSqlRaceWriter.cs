// <copyright file="MarkerSqlRaceWriter.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MESL.SqlRace.Domain;

using Stream.Api.Stream.Reader.Abstractions;

namespace Stream.Api.Stream.Reader.SqlRace.Writers
{
    public class MarkerSqlRaceWriter : BaseSqlRaceWriter
    {
        public MarkerSqlRaceWriter(IClientSession clientSession)
        {
            this.ClientSession = clientSession;
        }

        public override bool TryWrite(ISqlRaceDto data)
        {
            var markerDto = (ISqlRaceMarkerDto)data;
            var marker = new Marker(markerDto.Timestamp, markerDto.Name);
            if (this.ClientSession.Session.Markers.Contains(marker))
            {
                // Drop the marker if the same marker has already been added.
                return true;
            }
            try
            {
                this.ClientSession.Session.Markers.Add(marker);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to write marker packet due to {ex.Message}");
                return false;
            }

            return true;
        }
    }
}