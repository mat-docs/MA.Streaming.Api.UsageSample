using MA.Streaming.API;

namespace Stream.Api.Stream.Reader
{
    internal interface ISession
    {
        public void ReadPackets(CancellationToken cancellationToken, Connection connectionDetails);
        public void UpdateSessionInfo(GetSessionInfoResponse response);
        public void EndSession();
    }
}
