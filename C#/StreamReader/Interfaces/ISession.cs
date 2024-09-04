
namespace Stream.Api.Stream.Reader.Interfaces
{
    internal interface ISession
    {
        public bool SessionEnded { get; }
        public void StartSession(string sessionKey);
        public void EndSession();
    }
}
