// <copyright file="FrmMain.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using System.Diagnostics;
using System.Text.Json;

using MA.Streaming.Abstraction;
using MA.Streaming.API;
using MA.Streaming.Api.UsageSample.DataFormatManagement;
using MA.Streaming.Api.UsageSample.ReadAndWriteManagement;
using MA.Streaming.Api.UsageSample.SessionManagement;
using MA.Streaming.Api.UsageSample.Utility;
using MA.Streaming.Contracts;
using MA.Streaming.Core.Configs;
using MA.Streaming.Core.Routing;
using MA.Streaming.Proto.Client.Local;
using MA.Streaming.Proto.Client.Remote;
using MA.Streaming.Proto.ServerComponent;

using SessionInfo = MA.Streaming.Api.UsageSample.SessionManagement.SessionInfo;

namespace MA.Streaming.Api.UsageSample;

public partial class FrmMain : Form, ISessionManagementPresenterListener, IDataFormatManagementPresenterListener, IReadAndWriteManagementPresenterListener
{
    private bool initialise;

    private SessionManagementPresenter? sessionManagementPresenter;
    private DataFormatManagementPresenter? dataFormatManagementPresenter;
    private ReadAndWriteManagementPresenter? readAndWriteManagementPresenter;
    private readonly Dictionary<long, LatencyInfo> latencyInfos = new();

    public FrmMain()
    {
        this.InitializeComponent();
    }

    #region Deployment

    private void chbUseRemoteStreamApi_CheckedChanged(object sender, EventArgs e)
    {
        if (this.chbDeployKafka.Checked)
        {
            this.txtKafkaAddress.Text = this.chbUseRemoteStreamApi.Checked ? "kafka:9092" : "localhost:9094";
        }
    }

    private void rbtPartitionBased_CheckedChanged(object sender, EventArgs e)
    {
        this.grdPartitionMappings.Visible = this.rbtPartitionBased.Checked;
    }

    private void Form1_Load(object sender, EventArgs e)
    {
        this.AddDefaultStreams();
    }

    private void AddDefaultStreams()
    {
        this.grdPartitionMappings.Rows.Add(
            "Stream1",
            1);
        this.grdPartitionMappings.Rows.Add(
            "Stream2",
            2);
    }

    private void btnCreateDeployFiles_Click(object sender, EventArgs e)
    {
        var configuration = this.CreateConfiguration();
        if (configuration is null)
        {
            return;
        }

        const string ConfigsPath = "Deployment/Configs";
        if (!Directory.Exists(ConfigsPath))
        {
            Directory.CreateDirectory(ConfigsPath);
        }

        File.WriteAllText(
            $"{ConfigsPath}/AppConfig.json",
            JsonSerializer.Serialize(
                configuration,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
        new DockerComposeFileGenerator().Generate(
            "Deployment/docker-compose.yml",
            this.chbDeployKafka.Checked,
            this.chbRemoteKeyGeneratorService.Checked,
            this.chbUseRemoteStreamApi.Checked,
            (int)this.numTxtApiPort.Value,
            (int)this.numTxtPromtheusPort.Value);

        MessageBox.Show("Deployment files created");
    }

    private StreamingApiConfiguration? CreateConfiguration()
    {
        var streamCreationStrategy = this.rbtPartitionBased.Checked ? StreamCreationStrategy.PartitionBased : StreamCreationStrategy.TopicBased;
        var brokerUrl = this.txtKafkaAddress.Text;
        var partitionMappings = new List<PartitionMapping>();
        if (streamCreationStrategy == StreamCreationStrategy.PartitionBased)
        {
            var dataGridViewRows = this.grdPartitionMappings.Rows.Cast<DataGridViewRow>().Where(dataGridViewRow => !dataGridViewRow.IsNewRow).ToList();
            foreach (var dataGridViewRow in dataGridViewRows)
            {
                if (!int.TryParse(dataGridViewRow.Cells[this.ColPartition.Index].Value.ToString(), out var partition))
                {
                    MessageBox.Show("Invalid Partition Value");
                    return null;
                }

                var stream = dataGridViewRow.Cells[this.ColStream.Index].Value?.ToString() ?? "";
                if (string.IsNullOrEmpty(stream))
                {
                    MessageBox.Show("Invalid Stream Value");
                    return null;
                }

                partitionMappings.Add(new PartitionMapping(stream, partition));
            }
        }

        var batchingResponses = this.chbBatchResponse.Checked;
        var useRemoteKeyGenerator = this.chbRemoteKeyGeneratorService.Checked;
        var keyGeneratorServiceAddress = this.chbUseRemoteStreamApi.Checked ? "key-generator-service:15379" : "localhost:15379";
        var remoteKeyGeneratorServiceAddress = useRemoteKeyGenerator ? keyGeneratorServiceAddress : "";
        var streamApiPort = (int)this.numTxtApiPort.Value;
        var prometheusPort = (int)this.numTxtPromtheusPort.Value;
        return new StreamingApiConfiguration(
            streamCreationStrategy,
            brokerUrl,
            partitionMappings,
            streamApiPort,
            true,
            true,
            useRemoteKeyGenerator,
            remoteKeyGeneratorServiceAddress,
            batchingResponses,
            prometheusPort);
    }

    private void chbDeployKafka_CheckedChanged(object sender, EventArgs e)
    {
        this.txtKafkaAddress.Enabled = !this.chbDeployKafka.Checked;
        this.chbUseRemoteStreamApi_CheckedChanged(null, null);
    }

    private void btnDeploy_Click(object sender, EventArgs e)
    {
        if (MessageBox.Show($"Are sure to continue composing up the docker services?", "Continue?", MessageBoxButtons.YesNo) ==
            DialogResult.No)
        {
            return;
        }

        var fileInfo = new FileInfo("Deployment/docker-compose.yml");

        if (!fileInfo.Exists)
        {
            MessageBox.Show("Please Create Compose File First.");
            return;
        }

        new DockerComposeRunner().Run(fileInfo.FullName, "stream-api-sample");

        MessageBox.Show("Please check if docker compose services is running now.");
    }

    #endregion

    private void btnCreateNewSession_Click(object sender, EventArgs e)
    {
        this.sessionManagementPresenter?.CreateNewSession(
            this.txtCreateSessionDataSource.Text,
            this.txtCreateSessionType.Text,
            (uint)this.numTxtCreateSessionVersion.Value);
    }

    private void InitializeClient()
    {
        if (this.initialise)
        {
            MessageBox.Show("The connection has been successfully initialised");
            return;
        }

        Task.Run(
            () =>
            {
                while (!this.initialise)
                {
                    try
                    {
                        SessionManagementService.SessionManagementServiceClient? sessionManagementClient = null;
                        PacketWriterService.PacketWriterServiceClient? packetWriterManagementClient = null;
                        PacketReaderService.PacketReaderServiceClient? packetReaderManagementClient = null;
                        DataFormatManagerService.DataFormatManagerServiceClient? dataFormatManagementClient = null;
                        ConnectionManagerService.ConnectionManagerServiceClient? connectionManagementClient = null;
                        if (!this.chbUseRemoteStreamApi.Checked)
                        {
                            var streamingApiConfiguration = this.CreateConfiguration();
                            if (streamingApiConfiguration is null)
                            {
                                return;
                            }

                            StreamingApiClient.Initialise(streamingApiConfiguration, new CancellationTokenSourceProvider(), new KafkaBrokerAvailabilityChecker());
                            sessionManagementClient = StreamingApiClient.GetSessionManagementClient();
                            packetWriterManagementClient = StreamingApiClient.GetPacketWriterClient();
                            packetReaderManagementClient = StreamingApiClient.GetPacketReaderClient();
                            dataFormatManagementClient = StreamingApiClient.GetDataFormatManagerClient();
                            connectionManagementClient = StreamingApiClient.GetConnectionManagerClient();
                        }
                        else
                        {
                            RemoteStreamingApiClient.Initialise("localhost:13579");
                            sessionManagementClient = RemoteStreamingApiClient.GetSessionManagementClient();
                            packetWriterManagementClient = RemoteStreamingApiClient.GetPacketWriterClient();
                            packetReaderManagementClient = RemoteStreamingApiClient.GetPacketReaderClient();
                            dataFormatManagementClient = RemoteStreamingApiClient.GetDataFormatManagerClient();
                            connectionManagementClient = RemoteStreamingApiClient.GetConnectionManagerClient();
                        }

                        this.sessionManagementPresenter = new SessionManagementPresenter(sessionManagementClient, new WindowsFormLogger(), this);
                        this.dataFormatManagementPresenter = new DataFormatManagementPresenter(
                            dataFormatManagementClient,
                            packetReaderManagementClient,
                            connectionManagementClient,
                            new WindowsFormLogger(),
                            this);
                        this.readAndWriteManagementPresenter = new ReadAndWriteManagementPresenter(
                            packetWriterManagementClient,
                            packetReaderManagementClient,
                            connectionManagementClient,
                            this);
                        this.initialise = true;
                        this.grbSamples.Invoke(
                            () =>
                            {
                                this.grbSamples.Text = "Samples";
                                this.grbSamples.Enabled = true;
                            });
                    }
                    catch (Exception ex)
                    {
                        if (MessageBox.Show(
                                $"Error happned during connecting to stream api server please check the deployment.{Environment.NewLine}Do you want to retry agin in 5 seconds.",
                                "error",
                                MessageBoxButtons.YesNo) == DialogResult.No)
                        {
                            return;
                        }

                        File.AppendAllText("log.txt", $"{DateTime.Now} Error:{ex} {Environment.NewLine}");
                        new FrmLoading().ShowOnForm(this, TimeSpan.FromSeconds(5));
                    }
                }
            });
    }

    private void btnGoToDirectory_Click(object sender, EventArgs e)
    {
        Process.Start("explorer.exe", new DirectoryInfo("Deployment").FullName);
    }

    public void OnSessionsDataLoaded(string dataSource, IReadOnlyList<string> sessionKeys)
    {
        this.rtxbSessionKeys.Text = string.Join(Environment.NewLine, sessionKeys);
    }

    public void OnSessionCreated(string dataSource, string createdSessionKey)
    {
        MessageBox.Show($"A New Session With Key {createdSessionKey} Created");
    }

    public void OnAssociatedSessionKeyAdded(string parentSessionKey, string associatedAddedKey)
    {
        MessageBox.Show($"An Associated Session Key Added");
    }

    public void OnSessionIdentifierUpdated(string sessionKey, string newIdentifier)
    {
        MessageBox.Show("Session Identifier Updated");
    }

    public void OnSessionInfoFetched(SessionInfo sessionInfo)
    {
        this.rtxbSessionInfo.Text = JsonSerializer.Serialize(
            sessionInfo,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });
    }

    public void OnSessionStartNotification(string dataSource, string sessionKey)
    {
        this.rtxbNotifications.Invoke(
            () =>
            {
                this.rtxbNotifications.Text +=
                    $"{DateTime.Now.TimeOfDay}:=> New Session Created{Environment.NewLine}Key:{sessionKey}{Environment.NewLine}DataSource:{dataSource}{Environment.NewLine}-----------------{Environment.NewLine}";
            });
    }

    public void OnSessionStopNotification(string dataSource, string sessionKey)
    {
        this.rtxbNotifications.Invoke(
            () =>
            {
                this.rtxbNotifications.Text +=
                    $"{DateTime.Now.TimeOfDay}:=> Session Stopped{Environment.NewLine}Key:{sessionKey}{Environment.NewLine}DataSource:{dataSource}{Environment.NewLine}-----------------{Environment.NewLine}";
            });
    }

    private void btnAddAssociateKey_Click(object sender, EventArgs e)
    {
        this.sessionManagementPresenter?.AddAssociateSessionId(this.txtAddAssociateSessionKey.Text, this.txtAddAssociateAssociateKey.Text);
    }

    private void btnUpdateIdentifier_Click(object sender, EventArgs e)
    {
        this.sessionManagementPresenter?.UpdateSessionIdentifier(this.txtUpdateIdentifierSessionKey.Text, this.txtUpdateIdentifierNewIdentifier.Text);
    }

    private void btnGetSessionKeys_Click(object sender, EventArgs e)
    {
        this.rtxbSessionKeys.Clear();
        this.sessionManagementPresenter?.GetSessions(this.txtGetSessionKeysDataSource.Text);
    }

    private void btnGetSessionInfo_Click(object sender, EventArgs e)
    {
        this.rtxbSessionInfo.Clear();
        this.sessionManagementPresenter?.GetSessionInfo(this.txtGetSessionInfoSessionKey.Text);
    }

    private void btnGetEventFormatId_Click(object sender, EventArgs e)
    {
        this.dataFormatManagementPresenter?.GetEventDataFormatId(this.txtGetEventFormatIdDataSource.Text, this.txtGetEventFormatIdEventIdentifier.Text);
    }

    public void OnEventDataFormatFetched(string dataSource, string eventIdentifier, ulong dataFormatIdentifier)
    {
        this.txtGetEventFormatIdFetchedDataFormat.Text = dataFormatIdentifier.ToString();
    }

    public void OnEventFetched(string dataSource, string responseEvent)
    {
        this.txtGetEventFetchedEvent.Text = responseEvent;
    }

    public void OnParameterListDataFormatFetched(string dataSource, IReadOnlyList<string> parameterIdentifiers, ulong dataFormatIdentifier)
    {
        this.txtGetParamDataFormatFetchedDataFormat.Text = dataFormatIdentifier.ToString();
    }

    public void OnParameterListFetched(string dataSource, IReadOnlyList<string> responseParameters)
    {
        this.rtxtGetParameterListFetchedParameterList.Text = string.Join(Environment.NewLine, responseParameters);
    }

    public void OnAllDataFormatLoaded(IEnumerable<DataFormatDefinitionPacketDto> fetchedDataFormatDefinitionPacketsDto)
    {
        this.rtxtAllDefinitionPackets.Text = JsonSerializer.Serialize(
            fetchedDataFormatDefinitionPacketsDto,
            new JsonSerializerOptions
            {
                WriteIndented = true,
                IncludeFields = true
            });
    }

    private void btnGetEvent_Click(object sender, EventArgs e)
    {
        this.dataFormatManagementPresenter?.GetEventByDataFormatIds(this.txtGetEventDataSource.Text, this.txtGetEventDataFormatId.Text);
    }

    private void btnGetParamDataFormat_Click(object sender, EventArgs e)
    {
        var lstParams = (from DataGridViewRow dataGridViewRow in this.grdGetParamDataFormatParamList.Rows
            where !dataGridViewRow.IsNewRow
            select dataGridViewRow.Cells[this.ColParamForDataFormat.Index]?.Value).Where(i => i is not null).Select(value => value.ToString()).ToList();

        this.dataFormatManagementPresenter?.GetParameterListDataFormatId(this.txtGetParamDataFormatDataSource.Text, lstParams);
    }

    private void btnGetParameterList_Click(object sender, EventArgs e)
    {
        this.dataFormatManagementPresenter?.GetParameterListByDataFormatIds(this.txtGetParameterListDataSource.Text, this.txtGetParameterListDataFormatId.Text);
    }

    private void btnGetAllDefinitionPacket_Click(object sender, EventArgs e)
    {
        this.dataFormatManagementPresenter?.GetAllPacketDefinitions(this.txtGetAllDefinitionPacketDataSource.Text);
    }

    private void button1_Click(object sender, EventArgs e)
    {
        this.InitializeClient();
    }

    private void btnPublish_Click(object sender, EventArgs e)
    {
        if (this.chbBatchResponse.Checked)
        {
            this.readAndWriteManagementPresenter?.PublishUsingBatching(
                this.txtPublishDataSource.Text,
                this.txtPublishStream.Text,
                this.txtPublishSessionKey.Text,
                (uint)this.numTxtPublishTimes.Value,
                (uint)this.numTxtPublishMessageSize.Value);
        }
        else
        {
            this.readAndWriteManagementPresenter?.Publish(
                this.txtPublishDataSource.Text,
                this.txtPublishStream.Text,
                this.txtPublishSessionKey.Text,
                (uint)this.numTxtPublishTimes.Value,
                (uint)this.numTxtPublishMessageSize.Value);
        }

        this.pnlPublishReceiveInfo.Enabled = false;
        this.pnlPublishAction.Enabled = false;
    }

    public void OnRunStarted(RunInfo runInfo)
    {
        if (this.InvokeRequired)
        {
            this.Invoke(() => this.OnRunStarted(runInfo));
            return;
        }

        this.rtxtLog.Text += $"{DateTime.Now.TimeOfDay} => run({runInfo.RunId}) started.{Environment.NewLine}";
        this.latencyInfos.Add(runInfo.RunId, new LatencyInfo(runInfo));
        this.lstRunInfo.DisplayMember = "Id";
        this.lstRunInfo.DataSource = this.latencyInfos.Values.ToList();
    }

    public void OnPublishCompleted(RunInfo runInfo)
    {
        if (this.InvokeRequired)
        {
            this.Invoke(() => this.OnPublishCompleted(runInfo));
            return;
        }

        this.rtxtLog.Text += $"{DateTime.Now.TimeOfDay} => publishing ({runInfo.RunId}) is finished.{Environment.NewLine}";
    }

    public void OnMessagesReceived(RunInfo runInfo, uint numberOfReceivedMessages, double maxMessageLatency)
    {
        this.latencyInfos[runInfo.RunId].ReceiveLatencies.Add(new LatencyRecord(numberOfReceivedMessages, maxMessageLatency));
    }

    public void OnMessagesPublished(RunInfo runInfo, uint numberOfPublishedMessages, double maxMessageLatency)
    {
        this.latencyInfos[runInfo.RunId].PublishLatencies.Add(new LatencyRecord(numberOfPublishedMessages, maxMessageLatency));
    }

    public void OnReceiveComplete(RunInfo runInfo)
    {
        if (this.InvokeRequired)
        {
            this.Invoke(() => this.OnReceiveComplete(runInfo));
            return;
        }

        this.rtxtLog.Text += $"{DateTime.Now.TimeOfDay} => receiving completed.{Environment.NewLine}";
    }

    public void OnRunCompleted(RunInfo runInfo)
    {
        if (this.InvokeRequired)
        {
            this.Invoke(() => this.OnRunCompleted(runInfo));
            return;
        }

        this.rtxtLog.Text += $"{DateTime.Now.TimeOfDay} => run({runInfo.RunId}) completed.{Environment.NewLine}";
        this.lstRunInfo.Refresh();
        this.pnlPublishAction.Enabled = true;
        this.pnlPublishAction.Refresh();
    }

    private void lstRunInfo_MouseDoubleClick(object sender, MouseEventArgs e)
    {
        if (this.lstRunInfo.SelectedItem is not LatencyInfo latencyInfo)
        {
            return;
        }

        if (latencyInfo.RunInfo.Completed)
        {
            this.LogLatencies(latencyInfo);
        }
    }

    private void LogLatencies(LatencyInfo latencyInfo)
    {
        if (this.InvokeRequired)
        {
            this.Invoke(() => this.LogLatencies(latencyInfo));
            return;
        }

        var publishLatencies = latencyInfo.PublishLatencies.Skip(1).Select(i => i.MaxLatency).ToList();
        var receiveLatencies = latencyInfo.ReceiveLatencies.Skip(1).Select(i => i.MaxLatency).ToList();
        this.rtxtRunInfoResults.Text = $"""
                                        Id :{latencyInfo.RunInfo.RunId}

                                        Data Source: {latencyInfo.RunInfo.DataSource}

                                        Stream: {latencyInfo.RunInfo.Stream}

                                        Session Key:{latencyInfo.RunInfo.SessionKey}

                                        Max Publish Latency : {publishLatencies.Max()} ms

                                        Average Publish Latency: {publishLatencies.Average()} ms

                                        Min Publish Latency: {publishLatencies.Min()} ms

                                        Max e2e Latency:{receiveLatencies.Max()} ms

                                        Average e2e Latency: {receiveLatencies.Average()} ms

                                        Min e2e Latency:{receiveLatencies.Min()} ms

                                        Elapsed Time: {latencyInfo.RunInfo.ElapsedTime} ms

                                        Number Of Messages: {latencyInfo.RunInfo.NumberOfMessageToPublish} messages

                                        Throughput: {(latencyInfo.RunInfo.NumberOfMessageToPublish * latencyInfo.RunInfo.MessageSize) / 1024 / 1024 / (latencyInfo.RunInfo.ElapsedTime / 1000)} MB/s
                                        """;
    }

    private void btnCompleteSession_Click(object sender, EventArgs e)
    {
        this.sessionManagementPresenter?.CompleteSession(this.txtCompleteSessionSessionKey.Text);
    }

    private void btnSubscribeStartNotification_Click(object sender, EventArgs e)
    {
        this.sessionManagementPresenter?.SubscribeStartNotifications(this.txbNotificationDataSource.Text);
    }

    private void btnSubscribeStopNotification_Click(object sender, EventArgs e)
    {
        this.sessionManagementPresenter?.SubscribeStopNotifications(this.txbNotificationDataSource.Text);
    }
}