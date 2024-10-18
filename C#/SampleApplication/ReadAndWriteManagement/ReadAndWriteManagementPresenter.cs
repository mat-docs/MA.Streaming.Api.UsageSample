// <copyright file="ReadAndWriteManagementPresenter.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using Google.Protobuf;

using Grpc.Core;

using MA.Streaming.API;
using MA.Streaming.Core;
using MA.Streaming.OpenData;

namespace MA.Streaming.Api.UsageSample.ReadAndWriteManagement;

internal interface IReadAndWriteManagementPresenterListener
{
    void OnRunStarted(RunInfo runInfo);

    void OnPublishCompleted(RunInfo runInfo);

    void OnMessagesReceived(RunInfo runInfo, uint numberOfReceivedMessages, double maxMessageLatency);

    void OnMessagesPublished(RunInfo runInfo, uint numberOfPublishedMessages, double maxMessageLatency, double maxWaitInQueue);

    void OnReceiveComplete(RunInfo runInfo);

    void OnRunCompleted(RunInfo runInfo);
}

internal class ReadAndWriteManagementPresenter
{
    private readonly PacketWriterService.PacketWriterServiceClient packetWriterServiceClient;
    private readonly PacketReaderService.PacketReaderServiceClient readerServiceClient;
    private readonly ConnectionManagerService.ConnectionManagerServiceClient connectionManagerServiceClient;
    private readonly IReadAndWriteManagementPresenterListener listener;
    private readonly TimeAndSizeWindowBatchProcessor<DataPacketDetails> publishTimeWindowBatchProcessor;
    private readonly TimeAndSizeWindowBatchProcessor<ReadPacketsResponse> receiveTimeWindowBatchProcessor;
    private readonly Dictionary<long, RunInfo> runInfos = new();
    private Connection? receivingConnection;
    private IAsyncStreamReader<ReadPacketsResponse>? readerStream;
    private bool readerStreamInitialised;

    public ReadAndWriteManagementPresenter(
        PacketWriterService.PacketWriterServiceClient packetWriterServiceClient,
        PacketReaderService.PacketReaderServiceClient readerServiceClient,
        ConnectionManagerService.ConnectionManagerServiceClient connectionManagerServiceClient,
        IReadAndWriteManagementPresenterListener listener)
    {
        this.packetWriterServiceClient = packetWriterServiceClient;
        this.readerServiceClient = readerServiceClient;
        this.connectionManagerServiceClient = connectionManagerServiceClient;
        this.listener = listener;
        this.publishTimeWindowBatchProcessor = new TimeAndSizeWindowBatchProcessor<DataPacketDetails>(
            this.WriteBatchPackets,
            new CancellationTokenSource(),
            batchSize: 100000,
            timeWindowSize: 5);
        this.receiveTimeWindowBatchProcessor = new TimeAndSizeWindowBatchProcessor<ReadPacketsResponse>(this.ProcessReceive, new CancellationTokenSource());
    }

    public void Publish(string dataSource, string stream, string sessionKey, uint numberOfMessageToPublish, uint messageSize)
    {
        this.InitialiseReaderStream(dataSource, stream, sessionKey);
        var runInfo = this.CreateRunInfoAndInsertInToRunInfos(dataSource, stream, sessionKey, numberOfMessageToPublish, messageSize);
        this.StartRunInfo(runInfo);
        Task.Delay(50).Wait();
        this.listener.OnRunStarted(runInfo);
        _ = Task.Run(
            async () =>
            {
                for (var i = 0; i < numberOfMessageToPublish; i++)
                {
                    var sampleCustomObject = new SampleCustomObject(runInfo.RunId, (uint)i, messageSize);
                    var writeDataPacketsRequest = new WriteDataPacketsRequest
                    {
                        Details =
                        {
                            CreateDataPacketDetail(runInfo.RunId, dataSource, stream, sessionKey, sampleCustomObject)
                        }
                    };
                    await runInfo.Publish(writeDataPacketsRequest);
                    this.listener.OnMessagesPublished(runInfo, 1, (DateTime.Now - sampleCustomObject.CreationTime).TotalMilliseconds, 0);
                }
            });
    }

    public void PublishUsingBatching(string dataSource, string stream, string sessionKey, uint numberOfMessageToPublish, uint messageSize)
    {
        this.InitialiseReaderStream(dataSource, stream, sessionKey);
        var runInfo = this.CreateRunInfoAndInsertInToRunInfos(dataSource, stream, sessionKey, numberOfMessageToPublish, messageSize);
        this.StartRunInfo(runInfo);
        Task.Delay(50).Wait();
        this.listener.OnRunStarted(runInfo);
        _ = Task.Run(
            () =>
            {
                for (var i = 0; i < numberOfMessageToPublish; i++)
                {
                    var sampleCustomObject = new SampleCustomObject(runInfo.RunId, (uint)i, messageSize);
                    this.publishTimeWindowBatchProcessor.Add(CreateDataPacketDetail(runInfo.RunId, dataSource, stream, sessionKey, sampleCustomObject));
                }
            });
    }

    public void StartListening()
    {
        if (this.readerStream is null)
        {
            return;
        }

        _ = Task.Run(
            async () =>
            {
                try
                {
                    while (true)
                    {
                        while (await this.readerStream.MoveNext())
                        {
                            try
                            {
                                var readPacketsResponse = this.readerStream.Current;
                                this.receiveTimeWindowBatchProcessor.Add(readPacketsResponse);
                            }
                            catch (Exception ex)
                            {
                                await File.AppendAllTextAsync("log.txt", ex.ToString());
                            }
                        }

                        Task.Delay(10).Wait();
                    }
                }
                catch (Exception ex)
                {
                    await File.AppendAllTextAsync("log.txt", ex.ToString());
                }
            });
    }

    private void InitialiseReaderStream(string dataSource, string stream, string sessionKey)
    {
        if (this.readerStreamInitialised)
        {
            return;
        }

        var newConnectionResponse = this.connectionManagerServiceClient.NewConnection(
            new NewConnectionRequest
            {
                Details = new ConnectionDetails
                {
                    DataSource = dataSource,
                    EssentialsOffset = 0,
                    MainOffset = 0,
                    Session = sessionKey,
                    StreamOffsets =
                    {
                        0
                    },
                    Streams =
                    {
                        stream
                    }
                }
            });
        this.receivingConnection = newConnectionResponse.Connection;
        this.readerStream = this.readerServiceClient.ReadPackets(
            new ReadPacketsRequest
            {
                Connection = this.receivingConnection
            }).ResponseStream;
        this.StartListening();
        this.readerStreamInitialised = true;
    }

    private async Task WriteBatchPackets(IReadOnlyList<DataPacketDetails> dataPacketDetailsList)
    {
        try
        {
            var messageSessionKey = dataPacketDetailsList[0].Message.SessionKey;
            var firstItem = SampleCustomObject.Deserialize(dataPacketDetailsList[0].Message.Content.ToByteArray());
            var runInfo = this.runInfos[long.Parse(messageSessionKey)];
            var updatedTimeMessage = new SampleCustomObject(runInfo.RunId, firstItem.Order, (uint)firstItem.Content.Length);
            dataPacketDetailsList[0].Message = new Packet
            {
                SessionKey = runInfo.RunId.ToString(),
                Content = ByteString.CopyFrom(updatedTimeMessage.Serialize()),
                IsEssential = false,
                Type = nameof(updatedTimeMessage)
            };
            var writeDataPacketsRequest = new WriteDataPacketsRequest
            {
                Details =
                {
                    dataPacketDetailsList
                }
            };
            await runInfo.Publish(writeDataPacketsRequest);
            this.listener.OnMessagesPublished(
                runInfo,
                1,
                (DateTime.Now - updatedTimeMessage.CreationTime).TotalMilliseconds,
                (DateTime.Now - firstItem.CreationTime).TotalMilliseconds);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }

    private async Task ProcessReceive(IReadOnlyList<ReadPacketsResponse> responses)
    {
        try
        {
            foreach (var readPacketsResponse in responses)
            {
                if (!long.TryParse(readPacketsResponse.Response[0].Packet.SessionKey, out var runId))
                {
                    continue;
                }

                if (!this.runInfos.TryGetValue(runId, out var runInfo))
                {
                    continue;
                }

                runInfo.OnMessageReceived(readPacketsResponse);
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }

    private void StartRunInfo(RunInfo runInfo)
    {
        runInfo.MessageReceived += this.RunInfoMessageReceived;
        runInfo.ReceivedCompleted += this.RunInfo_ReceivedCompleted;
        runInfo.PublishedCompleted += this.RunInfo_PublishedCompleted;
    }

    private void RunInfo_PublishedCompleted(object? sender, DateTime e)
    {
        if (sender is not RunInfo runInfo)
        {
            return;
        }

        this.listener.OnPublishCompleted(runInfo);
    }

    private void RunInfo_ReceivedCompleted(object? sender, DateTime e)
    {
        if (sender is not RunInfo runInfo)
        {
            return;
        }

        this.listener.OnReceiveComplete(runInfo);
        this.listener.OnRunCompleted(runInfo);
    }

    private void RunInfoMessageReceived(object? sender, IReadOnlyList<PacketResponse> e)
    {
        if (sender is not RunInfo runInfo)
        {
            return;
        }

        var firstItem = SampleCustomObject.Deserialize(e[0].Packet.Content.ToByteArray());
        var latency = (DateTime.Now - firstItem.CreationTime).TotalMilliseconds;
        this.listener.OnMessagesReceived(runInfo, (uint)e.Count, latency);
    }

    private RunInfo CreateRunInfoAndInsertInToRunInfos(string dataSource, string stream, string sessionKey, uint numberOfMessageToPublish, uint messageSize)
    {
        var runId = DateTime.Now.Ticks;
        var runInfo = new RunInfo(
            runId,
            dataSource,
            stream,
            sessionKey,
            numberOfMessageToPublish,
            messageSize,
            this.packetWriterServiceClient);
        this.runInfos.Add(
            runId,
            runInfo);
        return runInfo;
    }

    private static DataPacketDetails CreateDataPacketDetail(long runId, string dataSource, string stream, string sessionKey, SampleCustomObject sampleCustomObject)
    {
        return new DataPacketDetails
        {
            DataSource = dataSource,
            SessionKey = sessionKey,
            Stream = stream,
            Message = new Packet
            {
                SessionKey = runId.ToString(),
                Content = ByteString.CopyFrom(sampleCustomObject.Serialize()),
                IsEssential = false,
                Type = nameof(sampleCustomObject)
            }
        };
    }
}