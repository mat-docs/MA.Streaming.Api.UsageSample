// <copyright file="SessionManagement.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using Stream.Api.Stream.Reader.Abstractions;
using Stream.Api.Stream.Reader.EventArguments;
using Stream.Api.Stream.Reader.SqlRace;

namespace Stream.Api.Stream.Reader
{
    internal class SessionManagement(StreamApiClient streamApiClient, Config config)
    {
        private readonly Dictionary<string, ISession> sessionKeyDictionary = [];

        public void GetLiveSessions()
        {
            if (streamApiClient.TryGetLiveSessions(out var sessionKeys))
            {
                foreach (var sessionKey in sessionKeys)
                {
                    Console.WriteLine($"New Live SqlRaceSession found with key {sessionKey}.");
                    this.CreateAndStartSession(sessionKey);
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
            foreach (var session in this.sessionKeyDictionary.Where(session => !session.Value.SessionEnded))
            {
                session.Value.EndSession();
                this.sessionKeyDictionary.Remove(session.Key);
            }
        }

        public void CreateAndStartSession(string sessionKey)
        {
            var sessionInfo = streamApiClient.GetSessionInfo(sessionKey);
            this.sessionKeyDictionary[sessionKey] = SqlRaceSessionFactory.CreateSession(sessionInfo, streamApiClient, config.SQLRaceConnectionString, sessionKey);
            this.sessionKeyDictionary[sessionKey].StartSession();
        }

        private void OnSessionStart(object? sender, SessionKeyEventArgs e)
        {
            Console.WriteLine($"New Live SqlRaceSession found with key {e.SessionKey}.");
            this.CreateAndStartSession(e.SessionKey);
            this.sessionKeyDictionary[e.SessionKey].StartSession();
        }

        private void OnSessionStop(object? sender, SessionKeyEventArgs e)
        {
            this.sessionKeyDictionary[e.SessionKey].EndSession();
            this.sessionKeyDictionary.Remove(e.SessionKey);
        }
    }
}