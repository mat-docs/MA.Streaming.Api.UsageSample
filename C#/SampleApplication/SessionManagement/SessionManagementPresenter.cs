// <copyright file="SessionManagementPresenter.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

using MA.Common.Abstractions;
using MA.Streaming.API;

namespace MA.Streaming.Api.UsageSample.SessionManagement;

internal interface ISessionManagementPresenterListener
{
    void OnSessionsDataLoaded(string dataSource, IReadOnlyList<string> sessionKeys);

    void OnSessionCreated(string dataSource, string createdSessionKey);

    void OnAssociatedSessionKeyAdded(string parentSessionKey, string associatedAddedKey);

    void OnSessionIdentifierUpdated(string sessionKey, string newIdentifier);

    void OnSessionInfoFetched(SessionInfo sessionInfo);

    void OnSessionStartNotification(string dataSource, string sessionKey);

    void OnSessionStopNotification(string dataSource, string sessionKey);
}

internal class SessionManagementPresenter
{
    private readonly SessionManagementService.SessionManagementServiceClient sessionManagementServiceClient;
    private readonly ILogger logger;
    private readonly ISessionManagementPresenterListener listener;
    private readonly CancellationTokenSource tokenSource = new();

    public SessionManagementPresenter(
        SessionManagementService.SessionManagementServiceClient sessionManagementServiceClient,
        ILogger logger,
        ISessionManagementPresenterListener listener)
    {
        this.sessionManagementServiceClient = sessionManagementServiceClient;
        this.logger = logger;
        this.listener = listener;
    }

    public void CreateNewSession(string dataSource, string type, uint version)
    {
        if (string.IsNullOrEmpty(dataSource))
        {
            this.logger.Error("Please Fill The Data Source");
            return;
        }

        var request = new CreateSessionRequest
        {
            DataSource = dataSource,
            Type = type,
            Version = version
        };
        try
        {
            var response = this.sessionManagementServiceClient.CreateSession(request);
            if (!string.IsNullOrEmpty(response.SessionKey))
            {
                this.listener.OnSessionCreated(dataSource, response.SessionKey);
            }
        }
        catch (Exception ex)
        {
            this.logger.Error($" Session Creation Exception:{ex} ");
        }
    }

    public void GetSessions(string dataSource)
    {
        if (string.IsNullOrEmpty(dataSource))
        {
            this.logger.Error("Please Fill The Data Source");
            return;
        }

        var request = new GetCurrentSessionsRequest
        {
            DataSource = dataSource
        };
        try
        {
            var response = this.sessionManagementServiceClient.GetCurrentSessions(request);
            if (response.SessionKeys.Any())
            {
                this.listener.OnSessionsDataLoaded(dataSource, response.SessionKeys);
            }
        }
        catch (Exception ex)
        {
            this.logger.Error($" Load Sessions Exception:{ex} ");
        }
    }

    public void AddAssociateSessionId(string sessionKey, string associateSessionKey)
    {
        if (string.IsNullOrEmpty(sessionKey))
        {
            this.logger.Error("Please Fill The Session Key");
            return;
        }

        if (string.IsNullOrEmpty(associateSessionKey))
        {
            this.logger.Error("Please Fill The Associate Key");
            return;
        }

        var request = new AddAssociateSessionRequest
        {
            SessionKey = sessionKey,
            AssociateSessionKey = associateSessionKey
        };
        try
        {
            var response = this.sessionManagementServiceClient.AddAssociateSession(request);
            if (response.Success)
            {
                this.listener.OnAssociatedSessionKeyAdded(sessionKey, associateSessionKey);
            }
        }
        catch (Exception ex)
        {
            this.logger.Error($" Add Associate Session Key Exception:{ex} ");
        }
    }

    public void UpdateSessionIdentifier(string sessionKey, string newIdentifier)
    {
        if (string.IsNullOrEmpty(sessionKey))
        {
            this.logger.Error("Please Fill The Session Key");
            return;
        }

        if (string.IsNullOrEmpty(newIdentifier))
        {
            this.logger.Error("Please Fill The Identifier");
            return;
        }

        var request = new UpdateSessionIdentifierRequest
        {
            SessionKey = sessionKey,
            Identifier = newIdentifier
        };
        try
        {
            var response = this.sessionManagementServiceClient.UpdateSessionIdentifier(request);
            if (response.Success)
            {
                this.listener.OnSessionIdentifierUpdated(sessionKey, newIdentifier);
            }
        }
        catch (Exception ex)
        {
            this.logger.Error($" Update Session Identifier Exception:{ex} ");
        }
    }

    public void GetSessionInfo(string sessionKey)
    {
        if (string.IsNullOrEmpty(sessionKey))
        {
            this.logger.Error("Please Fill The Session Key");
            return;
        }

        var request = new GetSessionInfoRequest
        {
            SessionKey = sessionKey
        };
        try
        {
            var response = this.sessionManagementServiceClient.GetSessionInfo(request);
            if (response == null)
            {
                return;
            }

            this.listener.OnSessionInfoFetched(
                new SessionInfo(
                    response.DataSource,
                    sessionKey,
                    response.Type,
                    response.Version,
                    response.AssociateSessionKeys,
                    response.Identifier,
                    response.IsComplete,
                    response.MainOffset,
                    response.EssentialsOffset,
                    response.Streams,
                    response.TopicPartitionOffsets.ToDictionary(i => i.Key, i => i.Value)));
        }
        catch (Exception ex)
        {
            this.logger.Error($" Update Session Identifier Exception:{ex} ");
        }
    }

    public void SubscribeStartNotifications(string dataSource)
    {
        var startNotificationStream = this.sessionManagementServiceClient.GetSessionStartNotification(
            new GetSessionStartNotificationRequest
            {
                DataSource = dataSource
            }).ResponseStream;
        _ = Task.Run(
            async () =>
            {
                try
                {
                    while (!this.tokenSource.Token.IsCancellationRequested)
                    {
                        while (await startNotificationStream.MoveNext(this.tokenSource.Token))
                        {
                            var notificationMessage = startNotificationStream.Current;
                            this.listener.OnSessionStartNotification(notificationMessage.DataSource, notificationMessage.SessionKey);
                        }
                    }
                }
                catch (Exception ex)
                {
                    await File.AppendAllTextAsync("log.txt", ex.ToString());
                }
            },
            this.tokenSource.Token);
    }

    public void SubscribeStopNotifications(string dataSource)
    {
        var stopNotificationStream = this.sessionManagementServiceClient.GetSessionStopNotification(
            new GetSessionStopNotificationRequest
            {
                DataSource = dataSource
            }).ResponseStream;
        _ = Task.Run(
            async () =>
            {
                try
                {
                    while (!this.tokenSource.Token.IsCancellationRequested)
                    {
                        while (await stopNotificationStream.MoveNext(this.tokenSource.Token))
                        {
                            var notificationMessage = stopNotificationStream.Current;
                            this.listener.OnSessionStopNotification(notificationMessage.DataSource, notificationMessage.SessionKey);
                        }
                    }
                }
                catch (Exception ex)
                {
                    await File.AppendAllTextAsync("log.txt", ex.ToString());
                }
            },
            this.tokenSource.Token);
    }

    public void CompleteSession(string sessionKey)
    {
        if (string.IsNullOrEmpty(sessionKey))
        {
            this.logger.Error("Please Fill The Session Key");
            return;
        }

        var request = new EndSessionRequest
        {
            SessionKey = sessionKey
        };
        try
        {
            this.sessionManagementServiceClient.EndSession(request);
            this.logger.Info("Session Completed Successfully");
        }
        catch (Exception ex)
        {
            this.logger.Error($" Complete Session Exception:{ex} ");
        }
    }
}