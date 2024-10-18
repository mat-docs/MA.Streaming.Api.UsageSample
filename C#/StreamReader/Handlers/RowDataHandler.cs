﻿// <copyright file="RowDataHandler.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using System.Collections.Concurrent;

using Google.Protobuf.Collections;

using MA.Streaming.Core;
using MA.Streaming.OpenData;

using Stream.Api.Stream.Reader.Abstractions;
using Stream.Api.Stream.Reader.SqlRace;
using Stream.Api.Stream.Reader.SqlRace.Mappers;
using Stream.Api.Stream.Reader.SqlRace.SqlRaceConfigProcessor;

namespace Stream.Api.Stream.Reader.Handlers
{
    internal class RowDataHandler : BaseHandler
    {
        private readonly ConcurrentQueue<RowDataPacket> rowDataQueue = new();
        private readonly ConcurrentDictionary<ulong, RepeatedField<string>> parameterListDataFormatCache = new();
        private readonly ISqlRaceWriter sessionWriter;
        private readonly StreamApiClient streamApiClient;
        private readonly SessionConfig sessionConfig;
        private readonly RowConfigProcessor configProcessor;
        private readonly RowPacketToSqlRaceParameterMapper rowMapper;
        private readonly TimeAndSizeWindowBatchProcessor<RowDataPacket> rowProcessor;

        public RowDataHandler(
            ISqlRaceWriter sessionWriter,
            StreamApiClient streamApiClient,
            SessionConfig sessionConfig,
            RowConfigProcessor configProcessor,
            RowPacketToSqlRaceParameterMapper rowMapper)
        {
            this.sessionWriter = sessionWriter;
            this.streamApiClient = streamApiClient;
            this.sessionConfig = sessionConfig;
            this.configProcessor = configProcessor;
            this.configProcessor.ProcessRowComplete += this.OnProcessRowComplete;
            this.rowMapper = rowMapper;
            this.rowProcessor = new TimeAndSizeWindowBatchProcessor<RowDataPacket>(this.ProcessPackets, new CancellationTokenSource(), 1000, 1);
        }

        public bool TryHandle(RowDataPacket packet)
        {
            this.rowProcessor.Add(packet);
            return true;
        }

        private RepeatedField<string> GetParameterList(ulong dataFormatId)
        {
            if (this.parameterListDataFormatCache.TryGetValue(dataFormatId, out RepeatedField<string>? parameterList))
            {
                return parameterList;
            }

            parameterList = this.streamApiClient.GetParameterList(dataFormatId);

            this.parameterListDataFormatCache[dataFormatId] = parameterList;

            return parameterList;
        }

        private void OnProcessRowComplete(object? sender, EventArgs e)
        {
            var dataQueue = this.rowDataQueue.ToArray();
            this.rowDataQueue.Clear();
            foreach (var packet in dataQueue)
            {
                this.TryHandle(packet);
            }
        }

        private Task ProcessPackets(IReadOnlyList<RowDataPacket> packets)
        {
            foreach (var packet in packets)
            {
                this.Update();
                RepeatedField<string> parameterList;
                try
                {
                    parameterList = packet.DataFormat.HasDataFormatIdentifier
                        ? this.GetParameterList(packet.DataFormat.DataFormatIdentifier)
                        : packet.DataFormat.ParameterIdentifiers.ParameterIdentifiers;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Parameter List Exception {ex}");
                    parameterList = new RepeatedField<string>();
                }

                var newParameters = parameterList
                    .Where(x => !this.sessionConfig.IsParameterExistInConfig(x))
                    .ToList();

                if (newParameters.Any())
                {
                    // If the packet contains new parameters, put it in the parameter list to add to config and queue the packet to process later.
                    this.configProcessor.AddParameterToConfig(newParameters);
                    this.rowDataQueue.Enqueue(packet);
                    continue;
                }

                var mappedParameters = this.rowMapper.MapParameter(packet, parameterList);
                if (mappedParameters.All(this.sessionWriter.TryWrite))
                {
                    continue;
                }

                this.rowDataQueue.Enqueue(packet);
            }
            return Task.CompletedTask;
        }
    }
}