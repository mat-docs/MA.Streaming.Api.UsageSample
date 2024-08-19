namespace Stream.Api.Stream.Reader.TextSessions
{
    public class ParameterText
    {
        public string Name { get; set; }
        public double Value { get; set; }
        public long Timestamp { get; set; }
    }

    public class ParameterSql : ParameterText
    {

    }
}
