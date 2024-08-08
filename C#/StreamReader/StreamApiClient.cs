using MA.Streaming.API;
using MA.Streaming.Proto.Client.Remote;

namespace Stream.Api.Stream.Reader
{
    internal class StreamApiClient
    {
        // This is the Stream API client that manages the sessions based off the calls given by the Stream API server.
        private readonly CancellationTokenSource cancellationTokenSourceSession = new CancellationTokenSource();
        private readonly CancellationTokenSource cancellationTokenSourceEvents = new CancellationTokenSource();
        private readonly AtlasSessionWriter atlasSessionWriter;
        private ConnectionManagerService.ConnectionManagerServiceClient? connectionManagerServiceClient;
        private SessionManagementService.SessionManagementServiceClient? sessionManagementServiceClient;
        private string rootFolderPath;
        private Dictionary<string, DateTime> streams = new Dictionary<string, DateTime>();
        private Dictionary<string, ISession> streamSessionKeyToSession = new Dictionary<string, ISession>();
        private AtlasSessionHandler atlasSessionHandler;
        private int outputFormat;
        private string previousSessionKey;
        private string dataSource;

        public StreamApiClient(string rootFolderPath, AtlasSessionWriter atlasSessionWriter, string dataSource, int outputFormat)
        {
            this.rootFolderPath = rootFolderPath;
            this.atlasSessionWriter = atlasSessionWriter;
            this.dataSource = dataSource;
            this.outputFormat = outputFormat;
        }

        /// <summary>
        /// Initialises the Stream API clients based off the given IP Address.
        /// </summary>
        /// <param name="serverAddress"></param>
        public void Initialise(string serverAddress)
        {
            Console.WriteLine("Initializing Stream API Client.");
            RemoteStreamingApiClient.Initialise(serverAddress);
            connectionManagerServiceClient = RemoteStreamingApiClient.GetConnectionManagerClient();
            sessionManagementServiceClient = RemoteStreamingApiClient.GetSessionManagementClient();
            this.atlasSessionHandler = new AtlasSessionHandler(atlasSessionWriter, dataSource);
        }

        /// <summary>
        /// Tries to Read the Stream API current sessions list to see if there is any live sessions mid run when the reader is started.
        /// </summary>
        /// <param name="dataSource"></param>
        /// <returns>True if a live session is found. Otherwise, it's False.</returns>
        public bool TryGetLiveSessions(string dataSource)
        {
            var foundLiveSession = false;
            var currentSessionsResponse =
                sessionManagementServiceClient.GetCurrentSessions(new GetCurrentSessionsRequest()
                { DataSource = dataSource });
            foreach (var session in currentSessionsResponse.SessionKeys.Reverse())
            {
                var sessionInfoResponse =
                    sessionManagementServiceClient.GetSessionInfo(new GetSessionInfoRequest() { SessionKey = session });
                if (sessionInfoResponse.IsComplete)
                {
                    continue;
                }
                Console.WriteLine($"Found Live session {sessionInfoResponse.Identifier}.");
                OnSessionStart(session);
                foundLiveSession = true;
            }
            return foundLiveSession;
        }

        /// <summary>
        /// If a live session is not found, then this will wait for a Session Start notification from the Stream API, so it can start Reading data from the server.
        /// </summary>
        /// <param name="dataSource"></param>
        /// <returns>True if session is found, False if there was an error in subscribing to the stream.</returns>
        public async Task<bool> SubscribeToStartSessionNotification(string dataSource)
        {
            var startNotificationStream = sessionManagementServiceClient.GetSessionStartNotification(
                new GetSessionStartNotificationRequest() { DataSource = dataSource }
            ).ResponseStream;
            var cancellationToken = cancellationTokenSourceEvents.Token;
            Console.WriteLine("Waiting for live session.");
            var task = await Task.Run(async () =>
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                        while (await startNotificationStream.MoveNext(cancellationToken))
                        {
                            var notificationMessage = startNotificationStream.Current;
                            OnSessionStart(notificationMessage.SessionKey);
                            return true;
                        }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to Subscribe to Start Notification due to {ex.Message}");
                }

                return false;
            }, cancellationToken);
            return task;
        }
        /// <summary>
        /// Subscribes to the Stop Notification stream to make sure that the recording stops when the session has finished recording by the Stream API.
        /// </summary>
        /// <param name="dataSource"></param>
        public void SubscribeToStopNotification(string dataSource)
        {
            var stopNotificationStream =
                sessionManagementServiceClient.GetSessionStopNotification(new GetSessionStopNotificationRequest()
                    { DataSource = dataSource }).ResponseStream;

            var cancellationToken = cancellationTokenSourceEvents.Token;
            Task.Run(async () =>
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    while (await stopNotificationStream.MoveNext(cancellationToken))
                    {
                        var stopNotificationResponse = stopNotificationStream.Current;
                        streamSessionKeyToSession[stopNotificationResponse.SessionKey].EndSession();
                        Console.WriteLine($"Session Ended {stopNotificationResponse.SessionKey}.");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"Failed to stop session with session key {stopNotificationStream.Current.SessionKey}.");
                }
            }, cancellationToken);
        }

        private readonly object SessionStartLock = new object();

        public void OnSessionStart(string sessionKey)
        {
            lock (SessionStartLock)
            {
                Console.WriteLine($"New Live Session found with key {sessionKey}.");
                var request = new GetSessionInfoRequest() { SessionKey = sessionKey };
                var sessionResponse = sessionManagementServiceClient.GetSessionInfo(request);
                if (this.outputFormat <= 2)
                {
                    var sqlSession = atlasSessionWriter.CreateSession(
                        sessionResponse.Identifier == "" ? "Untitled" : sessionResponse.Identifier, sessionResponse.Type);
                    if (this.outputFormat == 1)
                    {
                        streamSessionKeyToSession[sessionKey] = new BatchSession(this.atlasSessionHandler, sqlSession, sessionKey, atlasSessionWriter);
                    }
                    else
                    {
                        streamSessionKeyToSession[sessionKey] = new Session(sqlSession, sessionKey, atlasSessionWriter, dataSource);
                    }
                }
                else
                {
                    streamSessionKeyToSession[sessionKey] = new TextSession(this.rootFolderPath,
                        sessionResponse.Identifier == "" ? "Untitled" : sessionResponse.Identifier, sessionKey,
                        dataSource);
                }
                SubscribeToStopNotification(sessionResponse.DataSource);
                QuerySessionInfo(sessionKey);
            }
        }
        /// <summary>
        /// Updates which streams is available for listening to the connection details.
        /// Allows for dynamic topic creation and listening to all the topics related to that session.
        /// </summary>
        /// <param name="sessionKey"></param>
        /// <returns>True if the session is finished. False if it is not.</returns>
        public bool UpdateDataStreams(string sessionKey)
        {
            var request = new GetSessionInfoRequest() { SessionKey = sessionKey };
            var sessionResponse = sessionManagementServiceClient.GetSessionInfo(request);
            var newStreams = sessionResponse.Streams.Where(i => !streams.Keys.Contains(i)).ToList();
            streamSessionKeyToSession[sessionKey].UpdateSessionInfo(sessionResponse);
            if (newStreams.Any() || previousSessionKey != sessionKey)
            {
                previousSessionKey = sessionKey;
                _ = Task.Run(() =>
                {

                    foreach (var newStream in newStreams)
                    {
                        streams.Add(newStream, DateTime.Now);
                    }

                    var offsets = new List<Tuple<string, long>>();
                    foreach (var stream in newStreams)
                    {
                        var key = $"{sessionResponse.DataSource}.{stream}:[0]";
                        offsets.Add(sessionResponse.TopicPartitionOffsets.TryGetValue(key, out var offset)
                            ? new Tuple<string, long>(stream, offset)
                            : new Tuple<string, long>(stream, 0));
                    }
                    var connectionDetails = new ConnectionDetails()
                    {
                        DataSource = sessionResponse.DataSource,
                        Streams = { offsets.Select(i => i.Item1) },
                        StreamOffsets = { offsets.Select(i => i.Item2) },
                        Session = sessionKey
                    };
                    var connectionResponse = connectionManagerServiceClient.NewConnection(new NewConnectionRequest()
                        { Details = connectionDetails });
                    var cancellationToken = cancellationTokenSourceSession.Token;
                    streamSessionKeyToSession[sessionKey].ReadPackets(cancellationToken, connectionResponse.Connection);
                });
            }
            return sessionResponse.IsComplete;
        }

        public void QuerySessionInfo(string sessionKey)
        {
            var cancellationToken = cancellationTokenSourceSession.Token;
            bool finished;
            do
            {
                finished = UpdateDataStreams(sessionKey);
                Thread.Sleep(1000);
            } while (!cancellationToken.IsCancellationRequested && !finished);
        }
    }
}