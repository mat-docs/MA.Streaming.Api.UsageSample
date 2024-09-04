using System.Collections.Concurrent;
using MESL.SqlRace.Domain;

namespace Stream.Api.Stream.Reader.SqlRace
{
    internal class SessionConfig
    {
        public ConcurrentDictionary<string, uint> ChannelIdRowParameterDictionary { get; set; } 
        public ConcurrentDictionary<string, Dictionary<uint, uint>> ChannelIdPeriodicParameterDictionary { get; set; }
        public ConcurrentDictionary<string, EventDefinition> EventDefCache { get; set; }

        public SessionConfig()
        {
            ChannelIdPeriodicParameterDictionary = new ConcurrentDictionary<string, Dictionary<uint, uint>>();
            ChannelIdRowParameterDictionary = new ConcurrentDictionary<string, uint>();
            EventDefCache = new ConcurrentDictionary<string, EventDefinition>();
        }

        public void Dispose()
        {
            ChannelIdPeriodicParameterDictionary.Clear();
            ChannelIdRowParameterDictionary.Clear();
            EventDefCache.Clear();
        }
    }
}
