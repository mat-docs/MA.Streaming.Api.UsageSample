// <copyright file="SessionStopHandler.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using Grpc.Core;

using MA.Streaming.API;

using Stream.Api.Stream.Reader.EventArguments;

namespace Stream.Api.Stream.Reader.Handlers
{
    internal class SessionStopHandler
    {
        private readonly CancellationTokenSource tokenSource = new();

        public EventHandler<SessionKeyEventArgs>? SessionStop;

        public void WaitForSessionStop(IAsyncStreamReader<GetSessionStopNotificationResponse> stopNotificationStream)
        {
            var cancellationToken = this.tokenSource.Token;
            Task.Run(
                async () =>
                {
                    try
                    {
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            while (await stopNotificationStream.MoveNext(cancellationToken))
                            {
                                if (stopNotificationStream.Current == null)
                                {
                                    continue;
                                }

                                var stopNotificationResponse = stopNotificationStream.Current;
                                this.SessionStop?.Invoke(this, new SessionKeyEventArgs(stopNotificationResponse.SessionKey));
                                Console.WriteLine($"SqlRaceSession Ended {stopNotificationResponse.SessionKey}.");
                            }
                        }
                    }
                    catch (Exception)
                    {
                        Console.WriteLine(
                            $"Failed to stop session with session key {stopNotificationStream.Current?.SessionKey}.");
                        this.tokenSource.Dispose();
                    }
                },
                cancellationToken);
        }
    }
}