﻿// <copyright file="PeriodicDataHandler.cs" company="McLaren Applied Ltd.">
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
    internal class PeriodicDataHandler : BaseHandler
    {
        private readonly ConcurrentDictionary<ulong, RepeatedField<string>> parameterListDataFormatCache = new();
        private readonly ConcurrentQueue<PeriodicDataPacket> periodicDataQueue = new();
        private readonly ISqlRaceWriter sessionWriter;
        private readonly StreamApiClient streamApiClient;
        private readonly PeriodicConfigProcessor configProcessor;
        private readonly SessionConfig sessionConfig;
        private readonly PeriodicPacketToSqlRaceParameterMapper parameterMapper;
        private readonly TimeAndSizeWindowBatchProcessor<PeriodicDataPacket> periodicProcessor;

        public PeriodicDataHandler(
            ISqlRaceWriter sessionWriter,
            StreamApiClient streamApiClient,
            SessionConfig sessionConfig,
            PeriodicConfigProcessor configProcessor,
            PeriodicPacketToSqlRaceParameterMapper periodicMapper)
        {
            this.sessionWriter = sessionWriter;
            this.streamApiClient = streamApiClient;
            this.sessionConfig = sessionConfig;
            this.configProcessor = configProcessor;
            this.configProcessor.ProcessPeriodicComplete += this.OnProcessPeriodicComplete;
            this.parameterMapper = periodicMapper;
            this.periodicProcessor = new TimeAndSizeWindowBatchProcessor<PeriodicDataPacket>(this.ProcessPackets, new CancellationTokenSource(), 1000, 1);
        }

        public bool TryHandle(PeriodicDataPacket packet)
        {
            this.periodicProcessor.Add(packet);
            return true;
        }

        private RepeatedField<string> GetParameterList(ulong dataFormatId)
        {
            if (this.parameterListDataFormatCache.TryGetValue(dataFormatId, out var parameterList))
            {
                return parameterList;
            }

            parameterList = this.streamApiClient.GetParameterList(dataFormatId);

            this.parameterListDataFormatCache[dataFormatId] = parameterList;

            return parameterList;
        }

        private void OnProcessPeriodicComplete(object? sender, EventArgs e)
        {
            var dataQueue = this.periodicDataQueue.ToArray();
            this.periodicDataQueue.Clear();
            foreach (var packet in dataQueue)
            {
                this.TryHandle(packet);
            }
        }

        private Task ProcessPackets(IReadOnlyList<PeriodicDataPacket> packets)
        {
            foreach (var packet in packets)
            {
                this.Update();
                
                var parameterList = packet.DataFormat.HasDataFormatIdentifier
                    ? this.GetParameterList(packet.DataFormat.DataFormatIdentifier)
                    : packet.DataFormat.ParameterIdentifiers.ParameterIdentifiers;

                if (packet.Interval == 0)
                {
                    // There shouldn't be any packets that give you 0 interval.
                    Console.WriteLine($"The packet containing the parameter {parameterList.First()} has an interval of 0. Ignoring.");
                    continue;
                }
                var newParameters = parameterList
                    .Where(x => !this.sessionConfig.IsParameterExistInConfig(x, packet.Interval))
                    .ToList();

                if (newParameters.Any())
                {
                    // If the packet contains new parameters, put it in the list parameters to add to config and queue the packet to process later.
                    foreach (var parameter in newParameters)
                    {
                        this.configProcessor.AddPeriodicParameterToConfig(new Tuple<string, uint>(parameter, packet.Interval));
                    }

                    this.periodicDataQueue.Enqueue(packet);
                    continue;
                }

                var mappedParameters = this.parameterMapper.MapParameter(packet, parameterList);
                if (mappedParameters.All(this.sessionWriter.TryWrite))
                {
                    continue;
                }

                this.periodicDataQueue.Enqueue(packet);
            }

            return Task.CompletedTask;
        }
    }
}