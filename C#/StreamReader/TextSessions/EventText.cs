namespace Stream.Api.Stream.Reader.TextSessions
{
    public class EventText
    {
        public string EventIdentifier { get; set; }
        public long Timestamp { get; set; }
        public double Data1 { get; set; }
        public double Data2 { get; set; }
        public double Data3 { get; set; }
    }

    public class EventSql : EventText
    {
        public double[] Data { get; set; }
    }
}
