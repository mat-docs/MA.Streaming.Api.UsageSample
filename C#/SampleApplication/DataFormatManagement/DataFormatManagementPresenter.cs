// <copyright file="DataFormatManagementPresenter.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

using Grpc.Core;

using MA.Common.Abstractions;
using MA.Streaming.API;
using MA.Streaming.Contracts;
using MA.Streaming.OpenData;
using MA.Streaming.Proto.Core.Providers;

namespace MA.Streaming.Api.UsageSample.DataFormatManagement;

internal interface IDataFormatManagementPresenterListener
{
    void OnEventDataFormatFetched(string dataSource, string eventIdentifier, ulong dataFormatIdentifier);

    void OnEventFetched(string dataSource, string responseEvent);

    void OnParameterListDataFormatFetched(string dataSource, IReadOnlyList<string> parameterIdentifiers, ulong dataFormatIdentifier);

    void OnParameterListFetched(string dataSource, IReadOnlyList<string> responseParameters);

    void OnAllDataFormatLoaded(IEnumerable<DataFormatDefinitionPacketDto> fetchedDataFormatDefinitionPacketsDto);
}

internal class DataFormatManagementPresenter
{
    private readonly DataFormatManagerService.DataFormatManagerServiceClient dataFormatManagerServiceClient;
    private readonly PacketReaderService.PacketReaderServiceClient packetReaderServiceClient;
    private readonly ConnectionManagerService.ConnectionManagerServiceClient connectionManagerServiceClient;
    private readonly ILogger logger;
    private readonly IDataFormatManagementPresenterListener listener;

    public DataFormatManagementPresenter(
        DataFormatManagerService.DataFormatManagerServiceClient dataFormatManagerServiceClient,
        PacketReaderService.PacketReaderServiceClient packetReaderServiceClient,
        ConnectionManagerService.ConnectionManagerServiceClient connectionManagerServiceClient,
        ILogger logger,
        IDataFormatManagementPresenterListener listener)
    {
        this.dataFormatManagerServiceClient = dataFormatManagerServiceClient;
        this.packetReaderServiceClient = packetReaderServiceClient;
        this.connectionManagerServiceClient = connectionManagerServiceClient;
        this.logger = logger;
        this.listener = listener;
    }

    public void GetEventDataFormatId(string dataSource, string eventIdentifier)
    {
        if (string.IsNullOrEmpty(dataSource))
        {
            this.logger.Error("Please Fill The Data Source");
            return;
        }

        if (string.IsNullOrEmpty(eventIdentifier))
        {
            this.logger.Error("Please Fill The EventIdentifier");
            return;
        }

        var eventDataFormatIdRequest = new GetEventDataFormatIdRequest
        {
            DataSource = dataSource,
            Event = eventIdentifier
        };
        try
        {
            var response = this.dataFormatManagerServiceClient.GetEventDataFormatId(eventDataFormatIdRequest);
            if (response.DataFormatIdentifier > 0)
            {
                this.listener.OnEventDataFormatFetched(dataSource, eventIdentifier, response.DataFormatIdentifier);
            }
        }
        catch (Exception ex)
        {
            this.logger.Error($" Session Creation Exception:{ex} ");
        }
    }

    public void GetEventByDataFormatIds(string dataSource, string dataFormatId)
    {
        if (string.IsNullOrEmpty(dataSource))
        {
            this.logger.Error("Please Fill The Data Source");
            return;
        }

        if (string.IsNullOrEmpty(dataFormatId))
        {
            this.logger.Error("Please Fill The Data Format Id");
            return;
        }

        var getEventRequest = new GetEventRequest
        {
            DataSource = dataSource,
            DataFormatIdentifier = ulong.Parse(dataFormatId)
        };
        try
        {
            var response = this.dataFormatManagerServiceClient.GetEvent(getEventRequest);
            if (!string.IsNullOrEmpty(response.Event))
            {
                this.listener.OnEventFetched(dataSource, response.Event);
            }
        }
        catch (Exception ex)
        {
            this.logger.Error($" Load Sessions Exception:{ex} ");
        }
    }

    public void GetParameterListDataFormatId(string dataSource, List<string?> parameterIdentifiers)
    {
        if (string.IsNullOrEmpty(dataSource))
        {
            this.logger.Error("Please Fill The Data Source");
            return;
        }

        if (parameterIdentifiers.TrueForAll(string.IsNullOrEmpty))
        {
            this.logger.Error("Please Fill The Parameter List");
            return;
        }

        var parameterDataFormatIdRequest = new GetParameterDataFormatIdRequest
        {
            DataSource = dataSource,
            Parameters =
            {
                parameterIdentifiers
            }
        };
        try
        {
            var response = this.dataFormatManagerServiceClient.GetParameterDataFormatId(parameterDataFormatIdRequest);
            if (response.DataFormatIdentifier > 0)
            {
                this.listener.OnParameterListDataFormatFetched(dataSource, parameterIdentifiers, response.DataFormatIdentifier);
            }
        }
        catch (Exception ex)
        {
            this.logger.Error($" Session Creation Exception:{ex} ");
        }
    }

    public void GetParameterListByDataFormatIds(string dataSource, string dataFormatId)
    {
        if (string.IsNullOrEmpty(dataSource))
        {
            this.logger.Error("Please Fill The Data Source");
            return;
        }

        if (string.IsNullOrEmpty(dataFormatId))
        {
            this.logger.Error("Please Fill The Data Format Id");
            return;
        }

        var getParametersListRequest = new GetParametersListRequest
        {
            DataSource = dataSource,
            DataFormatIdentifier = ulong.Parse(dataFormatId)
        };
        try
        {
            var response = this.dataFormatManagerServiceClient.GetParametersList(getParametersListRequest);
            if (response.Parameters != null &&
                response.Parameters.Any(i => !string.IsNullOrEmpty(i)))
            {
                this.listener.OnParameterListFetched(dataSource, response.Parameters);
            }
        }
        catch (Exception ex)
        {
            this.logger.Error($" Load Sessions Exception:{ex} ");
        }
    }

    public void GetAllPacketDefinitions(string dataSource)
    {
        if (string.IsNullOrEmpty(dataSource))
        {
            this.logger.Error("Please Fill The Data Source");
            return;
        }

        var newConnectionResponse = this.connectionManagerServiceClient.NewConnection(
            new NewConnectionRequest
            {
                Details = new ConnectionDetails
                {
                    DataSource = dataSource
                }
            });

        var readEssentialsRequest = new ReadEssentialsRequest
        {
            Connection = newConnectionResponse.Connection
        };
        try
        {
            var lstPacketDefinitions = new List<DataFormatDefinitionPacket>();
            var readEssentialsStream = this.packetReaderServiceClient.ReadEssentials(readEssentialsRequest).ResponseStream;
            var type = new TypeNameProvider().DataFormatDefinitionPacketTypeName;
            while (readEssentialsStream.MoveNext().Result)
            {
                lstPacketDefinitions.AddRange(
                    from packetResponse in readEssentialsStream.Current.Response
                    where packetResponse.Packet.Type == type
                    select DataFormatDefinitionPacket.Parser.ParseFrom(packetResponse.Packet.Content));
            }

            if (lstPacketDefinitions.Any())
            {
                this.listener.OnAllDataFormatLoaded(
                    lstPacketDefinitions.Select(i =>
                    {
                        var parameterIdentifiers = i.HasEventIdentifier ? [i.EventIdentifier] : i.ParameterIdentifiers.ParameterIdentifiers;
                        return new DataFormatDefinitionPacketDto(
                            i.Identifier,
                            (DataFormatTypeDto)i.Type,
                            parameterIdentifiers,
                            string.Join(",", parameterIdentifiers));
                    }));
            }

            this.connectionManagerServiceClient.CloseConnection(
                new CloseConnectionRequest
                {
                    Connection = newConnectionResponse.Connection
                });
        }
        catch (Exception ex)
        {
            this.logger.Error($" Load Sessions Exception:{ex} ");
        }
    }
}