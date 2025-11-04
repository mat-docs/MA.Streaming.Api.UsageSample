// <copyright file="SessionStartHandler.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

using Grpc.Core;

using MA.Streaming.API;

using Stream.Api.Stream.Reader.EventArguments;

namespace Stream.Api.Stream.Reader.Handlers
{
    internal class SessionStartHandler
    {
        private readonly CancellationTokenSource tokenSource = new();

        public EventHandler<SessionKeyEventArgs>? NewSessionStart;

        public void WaitForSessionStart(IAsyncStreamReader<GetSessionStartNotificationResponse> startNotificationStream)
        {
            var cancellationToken = this.tokenSource.Token;
            Console.WriteLine("Waiting for live session.");
            _ = Task.Run(
                async () =>
                {
                    try
                    {
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            while (await startNotificationStream.MoveNext(cancellationToken))
                            {
                                var notificationMessage = startNotificationStream.Current;
                                this.NewSessionStart?.Invoke(
                                    this,
                                    new SessionKeyEventArgs(notificationMessage.SessionKey));
                            }
                        }
                    }
                    catch (RpcException ex) when (ex.Status.StatusCode == StatusCode.Cancelled)
                    {
                        Console.WriteLine("Start Notification Stream has been cancelled.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to Subscribe to Start Notification due to {ex.Message}");
                        this.tokenSource.Cancel();
                    }
                    finally
                    {
                        this.tokenSource.Dispose();
                    }
                },
                cancellationToken);
        }

        public void Stop()
        {
            this.tokenSource.Cancel();
        }
    }
}