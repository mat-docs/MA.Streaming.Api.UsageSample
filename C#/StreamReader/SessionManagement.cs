// <copyright file="SessionManagement.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

using MA.Streaming.API;

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
            if (this.TryGetLiveSessions(out var sessionKeys))
            {
                foreach (var sessionKey in sessionKeys)
                {
                    Console.WriteLine($"New Live SqlRaceSession found with key {sessionKey}.");
                    this.CreateAndStartSession(sessionKey);
                }
            }

            streamApiClient.SessionStart += this.OnSessionStart;
            streamApiClient.SubscribeToStartSessionNotification();

            streamApiClient.SessionStop += this.OnSessionStop;
            streamApiClient.SubscribeToStopNotification();
        }

        public void GetHistoricalSessions()
        {
            Console.WriteLine("Finding historical sessions in the broker.");
            if (!this.TryGetHistoricalSessions(out var sessions))
            {
                Console.WriteLine("No prerecorded sessions are found.");
                return;
            }

            Console.WriteLine(
                """
                Select a session by typing the number and press enter.

                The following sessions are available from the broker:
                """);
            for (var i = 0; i < sessions.Count; i++)
            {
                Console.WriteLine($"[{i}] {sessions[i].Item2.Identifier}");
            }

            var sessionSelected = Console.ReadLine();
            if (sessionSelected == null)
            {
                Console.WriteLine("Invalid input was given.");
                return;
            }

            var sessionIndex = int.Parse(sessionSelected);
            if (sessionIndex > sessions.Count ||
                sessionIndex < 0)
            {
                Console.WriteLine("Selection is outside the bounds of the array.");
                return;
            }

            this.CreateAndStartSession(sessions[sessionIndex].Item1);
            Task.Delay(1000).Wait();
            this.sessionKeyDictionary[sessions[sessionIndex].Item1].EndSession();
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
            if (sessionInfo == null)
            {
                Console.WriteLine($"Can't find the session with key:{sessionKey}");
                return;
            }

            this.sessionKeyDictionary[sessionKey] = SqlRaceSessionFactory.CreateSession(sessionInfo, streamApiClient, config.SQLRaceConnectionString, sessionKey);
            this.sessionKeyDictionary[sessionKey].StartSession();
        }

        public bool TryGetLiveSessions(out IReadOnlyList<string> liveSessions)
        {
            var foundLiveSession = false;
            var foundSessions = new List<string>();
            liveSessions = foundSessions;
            try
            {
                var currentSessionsResponse = streamApiClient.GetCurrentSessions();

                if (currentSessionsResponse == null)
                {
                    return foundLiveSession;
                }

                foreach (var session in currentSessionsResponse.SessionKeys.Reverse())
                {
                    var sessionInfoResponse = streamApiClient.GetSessionInfo(session);
                    if (sessionInfoResponse == null)
                    {
                        continue;
                    }

                    if (sessionInfoResponse.IsComplete)
                    {
                        continue;
                    }

                    Console.WriteLine($"Found Live session {sessionInfoResponse.Identifier}.");
                    foundLiveSession = true;
                    foundSessions.Add(session);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to get any current sessions due to {ex.Message}");
                return foundLiveSession;
            }

            return foundLiveSession;
        }

        public bool TryGetHistoricalSessions(out IReadOnlyList<Tuple<string, GetSessionInfoResponse>> historicalSessions)
        {
            var foundHistoricalSessions = false;
            var foundSessions = new List<Tuple<string, GetSessionInfoResponse>>();
            historicalSessions = foundSessions;
            try
            {
                var currentSessionResponse = streamApiClient.GetCurrentSessions();
                if (currentSessionResponse is null)
                {
                    return foundHistoricalSessions;
                }

                foreach (var sessionKey in currentSessionResponse.SessionKeys)
                {
                    var sessionInfoResponse = streamApiClient.GetSessionInfo(sessionKey);
                    if (sessionInfoResponse == null)
                    {
                        continue;
                    }

                    if (!sessionInfoResponse.IsComplete)
                    {
                        continue;
                    }

                    foundHistoricalSessions = true;
                    foundSessions.Add(new Tuple<string, GetSessionInfoResponse>(sessionKey, sessionInfoResponse));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to get sessions from the broker due to {ex.Message}");
                return foundHistoricalSessions;
            }

            return foundHistoricalSessions;
        }

        private void OnSessionStart(object? sender, SessionKeyEventArgs e)
        {
            Console.WriteLine($"New Live SqlRaceSession found with key {e.SessionKey}.");
            this.CreateAndStartSession(e.SessionKey);
        }

        private void OnSessionStop(object? sender, SessionKeyEventArgs e)
        {
            if (!this.sessionKeyDictionary.TryGetValue(e.SessionKey, out var session))
            {
                return;
            }

            session.EndSession();
            this.sessionKeyDictionary.Remove(e.SessionKey);
        }
    }
}