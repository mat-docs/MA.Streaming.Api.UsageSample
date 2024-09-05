using System.Collections.Concurrent;
using MESL.SqlRace.Domain;
using MA.Streaming.OpenData;
using Stream.Api.Stream.Reader.SqlRace;

namespace Stream.Api.Stream.Reader.Handlers
{
    internal class EventDataHandler(
        AtlasSessionWriter sessionWriter,
        StreamApiClient streamApiClient,
        IClientSession clientSession,
        ConfigurationProcessor configProcessor)
    {
        private ConcurrentDictionary<ulong, string> eventIdentifierDataFormatCache = new();
        public bool TryHandle(EventPacket packet)
        {
            var eventIdentifier = packet.DataFormat.HasDataFormatIdentifier
                ? this.GetEventIdentifier(packet.DataFormat.DataFormatIdentifier)
                : packet.DataFormat.EventIdentifier;

            if (!sessionWriter.IsEventExistInConfig(eventIdentifier, clientSession))
            {
                configProcessor.AddPacketEvent(eventIdentifier);
                return false;
            }

            var timestamp = (long)packet.Timestamp;
            var values = new List<double>();
            values.AddRange(packet.RawValues);
            if (sessionWriter.TryAddEvent(clientSession, eventIdentifier, timestamp, values))
            {
                return true;
            }
            Console.WriteLine($"Failed to write event {eventIdentifier}.");
            return false;
        }
        private string GetEventIdentifier(ulong dataFormatId)
        {
            if (this.eventIdentifierDataFormatCache.TryGetValue(dataFormatId, out var eventIdentifier))
                return eventIdentifier;

            eventIdentifier = streamApiClient.GetEventId(dataFormatId);
            this.eventIdentifierDataFormatCache[dataFormatId] = eventIdentifier;

            return eventIdentifier;
        }
    }
}
