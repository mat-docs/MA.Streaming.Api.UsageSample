

using MA.Streaming.OpenData;

namespace Stream.Api.Stream.Reader.Abstractions
{
    public interface IPacketHandler
    {
        public void Handle(Packet packet);
    }
}
