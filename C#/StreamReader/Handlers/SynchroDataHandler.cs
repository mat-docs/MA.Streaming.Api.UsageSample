// <copyright file="SynchroDataHandler.cs" company="McLaren Applied Ltd.">
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
    internal class SynchroDataHandler : BaseHandler
    {
        private readonly ConcurrentQueue<SynchroDataPacket> synchroDataQueue = new();
        private readonly ConcurrentDictionary<ulong, RepeatedField<string>> parameterListDataFormatCache = new();
        private readonly ISqlRaceWriter sessionWriter;
        private readonly StreamApiClient streamApiClient;
        private readonly SessionConfig sessionConfig;
        private readonly SynchroConfigProcessor configProcessor;
        private readonly SynchroPacketToSqlRaceSynchroMapper synchroMapper;
        private readonly TimeAndSizeWindowBatchProcessor<SynchroDataPacket> synchroProcessor;

        public SynchroDataHandler(
            ISqlRaceWriter sessionWriter,
            StreamApiClient streamApiClient,
            SessionConfig sessionConfig,
            SynchroConfigProcessor configProcessor,
            SynchroPacketToSqlRaceSynchroMapper synchroMapper)
        {
            this.sessionWriter = sessionWriter;
            this.streamApiClient = streamApiClient;
            this.sessionConfig = sessionConfig;
            this.configProcessor = configProcessor;
            this.configProcessor.ProcessSynchroComplete += this.OnConfigProcessComplete;
            this.synchroMapper = synchroMapper;
            this.synchroProcessor = new TimeAndSizeWindowBatchProcessor<SynchroDataPacket>(this.ProcessPackets, new CancellationTokenSource(), 100, 1);
        }

        public bool TryHandle(SynchroDataPacket packet)
        {
            this.synchroProcessor.Add(packet);
            return true;
        }

        private Task ProcessPackets(IReadOnlyList<SynchroDataPacket> packetList)
        {
            foreach (var packet in packetList)
            {
                this.Update();
                var parameterList = packet.DataFormat.HasDataFormatIdentifier
                    ? this.GetParameterList(packet.DataFormat.DataFormatIdentifier)
                    : packet.DataFormat.ParameterIdentifiers.ParameterIdentifiers;

                var newParameters = parameterList.Where(x => !this.sessionConfig.IsSynchroExistInConfig(x)).ToList();
                if (newParameters.Any())
                {
                    this.synchroDataQueue.Enqueue(packet);
                    this.configProcessor.AddSynchroParameterToConfig(parameterList);
                    continue;
                }

                var mappedParameters = this.synchroMapper.MapParameter(packet, parameterList);
                if (mappedParameters.All(this.sessionWriter.TryWrite))
                {
                    continue;
                }

                this.synchroDataQueue.Enqueue(packet);
            }

            return Task.CompletedTask;
        }

        private IReadOnlyList<string> GetParameterList(ulong dataFormat)
        {
            if (this.parameterListDataFormatCache.TryGetValue(dataFormat, out var parameterList))
            {
                return parameterList;
            }

            parameterList = this.streamApiClient.GetParameterList(dataFormat);
            this.parameterListDataFormatCache[dataFormat] = parameterList;
            return parameterList;
        }

        private void OnConfigProcessComplete(object? sender, EventArgs e)
        {
            var packetCopy = this.synchroDataQueue.ToArray();
            this.synchroDataQueue.Clear();
            foreach (var packet in packetCopy)
            {
                this.TryHandle(packet);
            }
        }
    }
}