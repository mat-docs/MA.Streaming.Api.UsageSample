

using Grpc.Core;
using MA.Streaming.API;
using Stream.Api.Stream.Reader.EventArguments;

namespace Stream.Api.Stream.Reader.Handlers
{
    internal class SessionStopHandler
    {
        private CancellationTokenSource tokenSource = new();

        public EventHandler<SessionKeyEventArgs>? SessionStop;

        public void WaitForSessionStop(IAsyncStreamReader<GetSessionStopNotificationResponse> stopNotificationStream)
        {
            var cancellationToken = tokenSource.Token;
            Task.Run(async () =>
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    while (await stopNotificationStream.MoveNext(cancellationToken))
                    {
                        if (stopNotificationStream.Current == null)
                        {
                            continue;
                        }
                        var stopNotificationResponse = stopNotificationStream.Current;
                        SessionStop?.Invoke(this, new SessionKeyEventArgs{SessionKey = stopNotificationResponse.SessionKey});
                        Console.WriteLine($"SqlRaceSession Ended {stopNotificationResponse.SessionKey}.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"Failed to stop session with session key {stopNotificationStream.Current?.SessionKey}.");
                }
            }, cancellationToken);

        }
    }
}
