

using Stream.Api.Stream.Reader.EventArguments;
using Stream.Api.Stream.Reader.Interfaces;
using Stream.Api.Stream.Reader.SqlRace;

namespace Stream.Api.Stream.Reader
{
    internal class SessionManagement(StreamApiClient streamApiClient, AtlasSessionWriter atlasSessionWriter)
    {
        private Dictionary<string, ISession> sessionKeyDictionary = new();
        public void GetLiveSessions()
        {
            if (streamApiClient.TryGetLiveSessions(out var sessionKey))
            {
                sessionKeyDictionary[sessionKey] = new SqlRaceSession(atlasSessionWriter, streamApiClient);
                sessionKeyDictionary[sessionKey].StartSession(sessionKey);
            }
            else
            {
                streamApiClient.SessionStart += OnSessionStart;
                streamApiClient.SubscribeToStartSessionNotification();
            }
            streamApiClient.SessionStop += OnSessionStop;
            streamApiClient.SubscribeToStopNotification();
        }

        public void CloseAllSessions()
        {
            foreach (var session in sessionKeyDictionary)
            {
                if (session.Value.SessionEnded)
                {
                    continue;
                }
                session.Value.EndSession();
                sessionKeyDictionary.Remove(session.Key);
            }
        }

        private void OnSessionStart(object? sender, SessionKeyEventArgs e)
        {
            sessionKeyDictionary[e.SessionKey] = new SqlRaceSession(atlasSessionWriter, streamApiClient);
            sessionKeyDictionary[e.SessionKey].StartSession(e.SessionKey);
        }

        private void OnSessionStop(object? sender, SessionKeyEventArgs e)
        {
            sessionKeyDictionary[e.SessionKey].EndSession();
            sessionKeyDictionary.Remove(e.SessionKey);
        }
    }
}
