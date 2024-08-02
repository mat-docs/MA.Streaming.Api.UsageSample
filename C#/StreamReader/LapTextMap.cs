using CsvHelper.Configuration;

namespace Stream.Api.Stream.Reader
{
    internal class LapTextMap: ClassMap<LapText>
    {
        public LapTextMap()
        {
            Map(m => m.Name).Index(0).Name("Name");
            Map(m => m.Timestamp).Index(1).Name("Timestamp");
            Map(m => m.Type).Index(2).Name("Type");
            Map(m => m.Description).Index(3).Name("Description");
            Map(m => m.Value).Index(4).Name("Value");
            Map(m => m.Source).Index(5).Name("Source");
        }
    }
}
