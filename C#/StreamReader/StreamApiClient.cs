// <copyright file="StreamApiClient.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using Google.Protobuf.Collections;
using Grpc.Core;
using MA.Streaming.API;
using MA.Streaming.Proto.Client.Remote;
using Stream.Api.Stream.Reader.EventArguments;
using Stream.Api.Stream.Reader.Handlers;
namespace Stream.Api.Stream.Reader
{
    // This is the Stream API client that manages the sessions based off the calls given by the Stream API server.
    internal class StreamApiClient
    {

        private ConnectionManagerService.ConnectionManagerServiceClient? connectionManagerServiceClient;
        private SessionManagementService.SessionManagementServiceClient? sessionManagementServiceClient;
        private PacketReaderService.PacketReaderServiceClient? packetReaderServiceClient;
        private DataFormatManagerService.DataFormatManagerServiceClient? dataFormatManagerServiceClient;
        private readonly string? dataSource;

        private SessionStartHandler sessionStartHandler;
        private SessionStopHandler sessionStopHandler;
        public EventHandler<SessionKeyEventArgs>? SessionStart;
        public EventHandler<SessionKeyEventArgs>? SessionStop;

        public StreamApiClient(Config config)
        {
            dataSource = config.dataSource;
        }

        /// <summary>
        ///     Initialises the Stream API clients based off the given IP Address.
        /// </summary>
        /// <param name="serverAddress"></param>
        public void Initialise(string serverAddress)
        {
            Console.WriteLine("Initializing Stream API Client.");
            RemoteStreamingApiClient.Initialise(serverAddress);
            connectionManagerServiceClient = RemoteStreamingApiClient.GetConnectionManagerClient();
            sessionManagementServiceClient = RemoteStreamingApiClient.GetSessionManagementClient();
            packetReaderServiceClient = RemoteStreamingApiClient.GetPacketReaderClient();
            dataFormatManagerServiceClient = RemoteStreamingApiClient.GetDataFormatManagerClient();
            sessionStartHandler = new SessionStartHandler();
            sessionStopHandler = new SessionStopHandler();
        }

        /// <summary>
        ///     Tries to Read the Stream API current sessions list to see if there is any live sessions mid run when the reader is
        ///     started.
        /// </summary>
        /// <param name="dataSource"></param>
        /// <returns>True if a live session is found. Otherwise, it's False.</returns>
        public bool TryGetLiveSessions(out string? sessionKey)
        {
            var foundLiveSession = false;
            try
            {
                var currentSessionsResponse =
                    sessionManagementServiceClient.GetCurrentSessions(new GetCurrentSessionsRequest()
                        { DataSource = dataSource });
                sessionKey = null;
                foreach (var session in currentSessionsResponse.SessionKeys.Reverse())
                {
                    var sessionInfoResponse = this.GetSessionInfo(session);
                    if (sessionInfoResponse.IsComplete) continue;
                    Console.WriteLine($"Found Live session {sessionInfoResponse.Identifier}.");
                    foundLiveSession = true;
                    sessionKey = session;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to get any current sessions due to {ex.Message}");
                sessionKey = null;
                return false;
            }
            
            return foundLiveSession;
        }

        public void SubscribeToStartSessionNotification()
        {
            var startNotificationStream = sessionManagementServiceClient?.GetSessionStartNotification(
                new GetSessionStartNotificationRequest() { DataSource = dataSource }).ResponseStream;

            if (startNotificationStream == null)
            {
                return;
            }

            sessionStartHandler.NewSessionStart += OnSessionStart;
            sessionStartHandler.WaitForSessionStart(startNotificationStream);
        }

        /// <summary>
        ///     Subscribes to the Stop Notification stream to make sure that the recording stops when the session has finished
        ///     recording by the Stream API.
        /// </summary>
        /// <param name="dataSource"></param>
        public void SubscribeToStopNotification()
        {
            var stopNotificationStream =
                sessionManagementServiceClient?.GetSessionStopNotification(new GetSessionStopNotificationRequest()
                    { DataSource = dataSource }).ResponseStream;

            if (stopNotificationStream == null)
            {
                return;
            }

            sessionStopHandler.SessionStop += OnSessionStop;
            sessionStopHandler.WaitForSessionStop(stopNotificationStream);
        }

        public RepeatedField<string> GetCurrentSessions(string dataSource)
        {
            return sessionManagementServiceClient
                .GetCurrentSessions(new GetCurrentSessionsRequest { DataSource = dataSource }).SessionKeys;
        }

        public GetSessionInfoResponse? GetSessionInfo(string sessionKey)
        {
            return sessionManagementServiceClient?.GetSessionInfo(new GetSessionInfoRequest
            {
                SessionKey = sessionKey
            });
        }

        public AsyncServerStreamingCall<ReadPacketsResponse> CreateReadPacketsStream(Connection connection)
        {
            return packetReaderServiceClient.ReadPackets(new ReadPacketsRequest { Connection = connection });
        }

        public RepeatedField<string> GetParameterList(ulong dataFormat)
        {
            return dataFormatManagerServiceClient.GetParametersList(new GetParametersListRequest
                { DataFormatIdentifier = dataFormat, DataSource = dataSource }).Parameters;
        }

        public string GetEventId(ulong dataFormat)
        {
            return dataFormatManagerServiceClient.GetEvent(new GetEventRequest
                { DataFormatIdentifier = dataFormat, DataSource = dataSource }).Event;
        }

        public Connection GetNewConnectionToSession(ConnectionDetails connectionDetails)
        {
            return connectionManagerServiceClient.NewConnection(
                new NewConnectionRequest { Details = connectionDetails }).Connection;
        }

        public ConnectionDetails GetConnectionDetails(Connection connection)
        {
            return connectionManagerServiceClient.GetConnection(new GetConnectionRequest { Connection = connection })
                .Details;
        }

        public bool TryCloseConnection(Connection connection)
        {
           return connectionManagerServiceClient.CloseConnection(new CloseConnectionRequest { Connection = connection }).Success;
        }

        private void OnSessionStart(object? sender, SessionKeyEventArgs e)
        {
            SessionStart?.Invoke(this, e);
        }

        private void OnSessionStop(object? sender, SessionKeyEventArgs e)
        {
            SessionStop?.Invoke(this, e);
        }
    }
}