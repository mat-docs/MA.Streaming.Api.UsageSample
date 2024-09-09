// <copyright file="PeriodicDataHandler.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using System.Collections.Concurrent;

using Google.Protobuf.Collections;

using MA.Streaming.OpenData;

using Stream.Api.Stream.Reader.Abstractions;
using Stream.Api.Stream.Reader.SqlRace;
using Stream.Api.Stream.Reader.SqlRace.Mappers;
using Stream.Api.Stream.Reader.SqlRace.SqlRaceConfigProcessor;

namespace Stream.Api.Stream.Reader.Handlers
{
    internal class PeriodicDataHandler
    {
        private readonly ConcurrentDictionary<ulong, RepeatedField<string>> parameterListDataFormatCache = new();
        private readonly ConcurrentQueue<PeriodicDataPacket> periodicDataQueue = new();
        private readonly ISqlRaceWriter sessionWriter;
        private readonly StreamApiClient streamApiClient;
        private readonly PeriodicConfigProcessor configProcessor;
        private readonly SessionConfig sessionConfig;
        private readonly PeriodicPacketToSqlRaceParameterMapper parameterMapper;

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
        }

        public bool TryHandle(PeriodicDataPacket packet)
        {
            var parameterList = packet.DataFormat.HasDataFormatIdentifier
                ? this.GetParameterList(packet.DataFormat.DataFormatIdentifier)
                : packet.DataFormat.ParameterIdentifiers.ParameterIdentifiers;

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
                return false;
            }

            var mappedParameters = this.parameterMapper.MapParameter(packet, parameterList);
            if (mappedParameters.All(this.sessionWriter.TryWrite))
            {
                return true;
            }

            this.periodicDataQueue.Enqueue(packet);
            return false;
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

        private void OnProcessPeriodicComplete(object? sender, EventArgs e)
        {
            var dataQueue = this.periodicDataQueue.ToArray();
            this.periodicDataQueue.Clear();
            foreach (var packet in dataQueue)
            {
                this.TryHandle(packet);
            }
        }
    }
}