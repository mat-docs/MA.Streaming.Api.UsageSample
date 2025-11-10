// <copyright file="StreamApiReader.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

using Grpc.Core;

using MA.Streaming.API;
using MA.Streaming.Core;
using MA.Streaming.OpenData;

using Stream.Api.Stream.Reader.Abstractions;

namespace Stream.Api.Stream.Reader.StreamApiReader
{
    internal class StreamApiReader : IStreamApiReader
    {
        private readonly Connection connection;
        private readonly CancellationTokenSource tokenSource = new();
        private readonly IPacketHandler<Packet> packetHandler;
        private readonly StreamApiClient streamApiClient;
        private readonly TimeAndSizeWindowBatchProcessor<Packet> packetProcessor;
        private DateTime lastUpdated;

        public StreamApiReader(Connection connection, IPacketHandler<Packet> packetHandler, StreamApiClient streamApiClient)
        {
            this.connection = connection;
            this.packetHandler = packetHandler;
            this.streamApiClient = streamApiClient;
            this.lastUpdated = DateTime.UtcNow;
            this.packetProcessor = new TimeAndSizeWindowBatchProcessor<Packet>(this.ProcessPackets, this.tokenSource, 1000, 1);
        }

        public void Start()
        {
            this.ReadPackets(this.connection, this.tokenSource.Token);
        }

        public void Stop()
        {
            do
            {
                Task.Delay(1000).Wait();
            }
            while (DateTime.UtcNow - this.lastUpdated < TimeSpan.FromSeconds(10));

            this.streamApiClient.TryCloseConnection(this.connection);
            this.tokenSource.Cancel();
            this.tokenSource.Dispose();
        }

        private void ReadPackets(Connection? connectionDetails, CancellationToken cancellationToken)
        {
            var streamReader = this.CreateStream(connectionDetails)?.ResponseStream;

            if (streamReader == null)
            {
                return;
            }

            _ = Task.Run(
                async () =>
                {
                    try
                    {
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            while (await streamReader.MoveNext(cancellationToken))
                            {
                                var packetResponse = streamReader.Current;
                                foreach (var response in packetResponse.Response)
                                {
                                    this.packetProcessor.Add(response.Packet);
                                    this.lastUpdated = DateTime.UtcNow;
                                }
                            }
                        }
                    }
                    catch (RpcException ex) when (ex.Status.StatusCode == StatusCode.Cancelled)
                    {
                        Console.WriteLine("Stream has been cancelled.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to read stream due to {ex}");
                        this.Stop();
                    }
                },
                cancellationToken);
        }

        private AsyncServerStreamingCall<ReadPacketsResponse>? CreateStream(
            Connection? connectionDetails)
        {
            return this.streamApiClient.CreateReadPacketsStream(connectionDetails);
        }

        private Task ProcessPackets(IReadOnlyList<Packet> packets)
        {
            foreach (var packet in packets)
            {
                this.packetHandler.Handle(packet);
            }

            return Task.CompletedTask;
        }
    }
}