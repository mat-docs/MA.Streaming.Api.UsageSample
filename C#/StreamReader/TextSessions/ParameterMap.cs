using CsvHelper.Configuration;

namespace Stream.Api.Stream.Reader.TextSessions
{
    public class ParameterMap : ClassMap<ParameterText>
    {
        public ParameterMap()
        {
            Map(m => m.Timestamp).Index(0).Name("Timestamp");
            Map(m => m.Name).Index(1).Name("Parameter Name");
            Map(m => m.Value).Index(2).Name("Value");
        }
    }
}
