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
    // This is the Stream API client that manages the communications between the program and the Stream API server.
    internal class StreamApiClient
    {
        private readonly Config config;
        private readonly SessionStartHandler sessionStartHandler;
        private readonly SessionStopHandler sessionStopHandler;

        private ConnectionManagerService.ConnectionManagerServiceClient? connectionManagerServiceClient;
        private SessionManagementService.SessionManagementServiceClient? sessionManagementServiceClient;
        private PacketReaderService.PacketReaderServiceClient? packetReaderServiceClient;
        private DataFormatManagerService.DataFormatManagerServiceClient? dataFormatManagerServiceClient;
        public EventHandler<SessionKeyEventArgs>? SessionStart;
        public EventHandler<SessionKeyEventArgs>? SessionStop;

        public StreamApiClient(Config config)
        {
            this.config = config;
            this.sessionStartHandler = new SessionStartHandler();
            this.sessionStopHandler = new SessionStopHandler();
        }

        /// <summary>
        ///     Initialises the Stream API clients based off the given IP Address.
        /// </summary>
        public void Initialise()
        {
            Console.WriteLine("Initializing Stream API Client.");
            RemoteStreamingApiClient.Initialise(this.config.IPAddress);
            this.connectionManagerServiceClient = RemoteStreamingApiClient.GetConnectionManagerClient();
            this.sessionManagementServiceClient = RemoteStreamingApiClient.GetSessionManagementClient();
            this.packetReaderServiceClient = RemoteStreamingApiClient.GetPacketReaderClient();
            this.dataFormatManagerServiceClient = RemoteStreamingApiClient.GetDataFormatManagerClient();
        }

        /// <summary>
        ///     Tries to Read the Stream API current sessions list to see if there is any live sessions mid run when the reader is
        ///     started.
        /// </summary>
        /// <returns>True if a live session is found. Otherwise, it's False.</returns>
        public bool TryGetLiveSessions(out List<string> sessionKey)
        {
            var foundLiveSession = false;
            sessionKey = [];
            try
            {
                var currentSessionsResponse = this.sessionManagementServiceClient?.GetCurrentSessions(
                    new GetCurrentSessionsRequest
                    {
                        DataSource = this.config.DataSource
                    });

                if (currentSessionsResponse == null)
                {
                    return false;
                }

                foreach (var session in currentSessionsResponse.SessionKeys.Reverse())
                {
                    var sessionInfoResponse = this.GetSessionInfo(session);
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
                    sessionKey.Add(session);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to get any current sessions due to {ex.Message}");
                return false;
            }

            return foundLiveSession;
        }

        public void SubscribeToStartSessionNotification()
        {
            var startNotificationStream = this.sessionManagementServiceClient?.GetSessionStartNotification(
                new GetSessionStartNotificationRequest
                {
                    DataSource = this.config.DataSource
                }).ResponseStream;

            if (startNotificationStream == null)
            {
                return;
            }

            this.sessionStartHandler.NewSessionStart += this.OnSessionStart;
            this.sessionStartHandler.WaitForSessionStart(startNotificationStream);
        }

        /// <summary>
        ///     Subscribes to the Stop Notification stream to make sure that the recording stops when the session has finished
        ///     recording by the Stream API.
        /// </summary>
        public void SubscribeToStopNotification()
        {
            var stopNotificationStream = this.sessionManagementServiceClient?.GetSessionStopNotification(
                new GetSessionStopNotificationRequest
                {
                    DataSource = this.config.DataSource
                }).ResponseStream;

            if (stopNotificationStream == null)
            {
                return;
            }

            this.sessionStopHandler.SessionStop += this.OnSessionStop;
            this.sessionStopHandler.WaitForSessionStop(stopNotificationStream);
        }

        public GetSessionInfoResponse? GetSessionInfo(string sessionKey)
        {
            var getSessionInfoResponse = this.sessionManagementServiceClient?.GetSessionInfo(
                new GetSessionInfoRequest
                {
                    SessionKey = sessionKey
                });
            if (getSessionInfoResponse is null)
            {
                Console.WriteLine("Can't get the session info");
            }

            return getSessionInfoResponse;
        }

        public AsyncServerStreamingCall<ReadPacketsResponse>? CreateReadPacketsStream(Connection? connection)
        {
            var asyncServerStreamingCall = this.packetReaderServiceClient?.ReadPackets(
                new ReadPacketsRequest
                {
                    Connection = connection
                });
            if (asyncServerStreamingCall is null)
            {
                Console.WriteLine("Can't create the read packet stream");
            }

            return asyncServerStreamingCall;
        }

        public RepeatedField<string> GetParameterList(ulong dataFormat)
        {
            var getParametersListResponse = this.dataFormatManagerServiceClient?.GetParametersList(
                new GetParametersListRequest
                {
                    DataFormatIdentifier = dataFormat,
                    DataSource = this.config.DataSource
                });
            if (getParametersListResponse is null)
            {
                Console.WriteLine("Can't get parameter list info");
            }

            return getParametersListResponse
                ?.Parameters ?? [];
        }

        public string GetEventId(ulong dataFormat)
        {
            var getEventResponse = this.dataFormatManagerServiceClient?.GetEvent(
                new GetEventRequest
                {
                    DataFormatIdentifier = dataFormat,
                    DataSource = this.config.DataSource
                });
            if (getEventResponse is null)
            {
                Console.WriteLine("Can't get event info");
            }

            return getEventResponse
                ?.Event ?? string.Empty;
        }

        public Connection? GetNewConnectionToSession(ConnectionDetails connectionDetails)
        {
            var newConnectionResponse = this.connectionManagerServiceClient?.NewConnection(
                new NewConnectionRequest
                {
                    Details = connectionDetails
                });
            if (newConnectionResponse is null)
            {
                Console.WriteLine("Can't create new connection");
            }

            return newConnectionResponse?.Connection;
        }

        public ConnectionDetails? GetConnectionDetails(Connection connection)
        {
            var getConnectionResponse = this.connectionManagerServiceClient?.GetConnection(
                new GetConnectionRequest
                {
                    Connection = connection
                });
            if (getConnectionResponse is null)
            {
                Console.WriteLine("Can't get the connection");
            }

            return getConnectionResponse
                ?.Details;
        }

        public bool TryCloseConnection(Connection? connection)
        {
            return this.connectionManagerServiceClient?.CloseConnection(
                new CloseConnectionRequest
                {
                    Connection = connection
                }).Success ?? false;
        }

        private void OnSessionStart(object? sender, SessionKeyEventArgs e)
        {
            this.SessionStart?.Invoke(this, e);
        }

        private void OnSessionStop(object? sender, SessionKeyEventArgs e)
        {
            this.SessionStop?.Invoke(this, e);
        }
    }
}