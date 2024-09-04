using System.Collections.Concurrent;
using MESL.SqlRace.Domain;

namespace Stream.Api.Stream.Reader.SqlRace
{
    internal class SessionConfig
    {
        public ConcurrentDictionary<string, uint> ChannelIdRowParameterDictionary { get; } = new();
        public ConcurrentDictionary<string, Dictionary<uint, uint>> ChannelIdPeriodicParameterDictionary { get; } = new();
        public ConcurrentDictionary<string, EventDefinition> EventDefCache { get; } = new();

        public void Dispose()
        {
            ChannelIdPeriodicParameterDictionary.Clear();
            ChannelIdRowParameterDictionary.Clear();
            EventDefCache.Clear();
        }
    }
}
