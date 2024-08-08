using CsvHelper.Configuration;

namespace Stream.Api.Stream.Reader
{
    public class EventTextMap : ClassMap<EventText>
    {
        public EventTextMap()
        {
            Map(m => m.Timestamp).Index(0).Name("Timestamp");
            Map(m => m.EventIdentifier).Index(1).Name("Event Identifier");
            Map(m => m.Data1).Index(2).Name("Data1");
            Map(m => m.Data2).Index(3).Name("Data2");
            Map(m => m.Data3).Index(4).Name("Data3");
        }
    }
}
