// <copyright file="SessionManagement.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using Stream.Api.Stream.Reader.Abstractions;
using Stream.Api.Stream.Reader.EventArguments;
using Stream.Api.Stream.Reader.SqlRace;

namespace Stream.Api.Stream.Reader
{
    internal class SessionManagement(StreamApiClient streamApiClient, AtlasSessionWriter atlasSessionWriter)
    {
        private readonly Dictionary<string, ISession> sessionKeyDictionary = [];

        public void GetLiveSessions()
        {
            if (streamApiClient.TryGetLiveSessions(out var sessionKeys))
            {
                foreach (var sessionKey in sessionKeys)
                {
                    this.sessionKeyDictionary[sessionKey] = new SqlRaceSession(atlasSessionWriter, streamApiClient);
                    this.sessionKeyDictionary[sessionKey].StartSession(sessionKey);
                }
            }
            else
            {
                streamApiClient.SessionStart += this.OnSessionStart;
                streamApiClient.SubscribeToStartSessionNotification();
            }

            streamApiClient.SessionStop += this.OnSessionStop;
            streamApiClient.SubscribeToStopNotification();
        }

        public void CloseAllSessions()
        {
            foreach (var session in this.sessionKeyDictionary)
            {
                if (session.Value.SessionEnded)
                {
                    continue;
                }

                session.Value.EndSession();
                this.sessionKeyDictionary.Remove(session.Key);
            }
        }

        private void OnSessionStart(object? sender, SessionKeyEventArgs e)
        {
            this.sessionKeyDictionary[e.SessionKey] = new SqlRaceSession(atlasSessionWriter, streamApiClient);
            this.sessionKeyDictionary[e.SessionKey].StartSession(e.SessionKey);
        }

        private void OnSessionStop(object? sender, SessionKeyEventArgs e)
        {
            this.sessionKeyDictionary[e.SessionKey].EndSession();
            this.sessionKeyDictionary.Remove(e.SessionKey);
        }
    }
}