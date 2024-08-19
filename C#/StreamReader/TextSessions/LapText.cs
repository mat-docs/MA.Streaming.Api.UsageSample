namespace Stream.Api.Stream.Reader.TextSessions
{
    public class LapText
    {
        public ulong Timestamp { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public string Source { get; set; }
        public long Value { get; set; }
    }

    public class LapSql : LapText
    {
    }
}
