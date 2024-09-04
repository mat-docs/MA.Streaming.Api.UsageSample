using MESL.SqlRace.Domain;
using MA.Streaming.OpenData;
using Stream.Api.Stream.Reader.SqlRace;

namespace Stream.Api.Stream.Reader.Handlers
{
    internal class MarkerHandler(
        AtlasSessionWriter sessionWriter,
        StreamApiClient streamApiClient,
        IClientSession clientSession
        )
    {
        public bool TryHandle(MarkerPacket packet)
        {
            var timestamp = (long)packet.Timestamp;
            if (packet.Type == "Lap Trigger")
                sessionWriter.AddLap(clientSession, timestamp, (short)packet.Value, packet.Label, true);
            else
                sessionWriter.AddMarker(clientSession, timestamp, packet.Label);
            return true;
        }
    }
}
