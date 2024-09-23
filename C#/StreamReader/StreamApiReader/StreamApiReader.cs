﻿// <copyright file="StreamApiReader.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

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
        private readonly CancellationTokenSource tokenSource = new CancellationTokenSource();
        private readonly IPacketHandler packetHandler;
        private readonly StreamApiClient streamApiClient;
        private DateTime lastUpdated;
        private readonly TimeAndSizeWindowBatchProcessor<Packet> packetProcessor;

        public StreamApiReader(Connection connection, IPacketHandler packetHandler, StreamApiClient streamApiClient)
        {
            this.connection = connection;
            this.packetHandler = packetHandler;
            this.streamApiClient = streamApiClient;
            this.lastUpdated = DateTime.Now;
            this.packetProcessor = new TimeAndSizeWindowBatchProcessor<Packet>(this.ProcessPackets, new CancellationTokenSource(), 1000, 1);
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
            while (DateTime.Now - this.lastUpdated < TimeSpan.FromSeconds(10));

            this.streamApiClient.TryCloseConnection(this.connection);
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
                                    this.lastUpdated = DateTime.Now;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to read stream due to {ex}");
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