
using Grpc.Core;
using MA.Streaming.API;
using Stream.Api.Stream.Reader.EventArguments;

namespace Stream.Api.Stream.Reader.Handlers
{
    internal class SessionStartHandler
    {
        private CancellationTokenSource tokenSource = new();

        public EventHandler<SessionKeyEventArgs>? NewSessionStart;
        public void WaitForSessionStart(IAsyncStreamReader<GetSessionStartNotificationResponse> startNotificationStream)
        {
            var cancellationToken = this.tokenSource.Token;
            Console.WriteLine("Waiting for live session.");
            _ = Task.Run(async () =>
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    while (await startNotificationStream.MoveNext(cancellationToken))
                    {
                        var notificationMessage = startNotificationStream.Current;
                        this.NewSessionStart?.Invoke(this, new SessionKeyEventArgs{SessionKey = notificationMessage.SessionKey});
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to Subscribe to Start Notification due to {ex.Message}");
                }
            }, cancellationToken);
        }
    }
}
