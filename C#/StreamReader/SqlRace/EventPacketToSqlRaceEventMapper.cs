using MA.Streaming.OpenData;
using Stream.Api.Stream.Reader.Abstractions;

namespace Stream.Api.Stream.Reader.SqlRace;

public class EventPacketToSqlRaceEventMapper : IEventPacketToSqlRaceEventMapper
{
    public SqlRaceEvent Map(EventPacket data)
    {
        throw new NotImplementedException();
    }
}