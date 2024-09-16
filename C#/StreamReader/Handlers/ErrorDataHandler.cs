// <copyright file="ErrorDataHandler.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using System.Collections.Concurrent;

using MA.Streaming.Core;
using MA.Streaming.OpenData;

using Stream.Api.Stream.Reader.Abstractions;
using Stream.Api.Stream.Reader.SqlRace;
using Stream.Api.Stream.Reader.SqlRace.Mappers;
using Stream.Api.Stream.Reader.SqlRace.SqlRaceConfigProcessor;

namespace Stream.Api.Stream.Reader.Handlers
{
    internal class ErrorDataHandler : BaseHandler
    {
        private readonly ISqlRaceWriter sessionWriter;
        private readonly SessionConfig sessionConfig;
        private readonly ConcurrentQueue<ErrorPacket> errorPacketQueue;
        private readonly ErrorConfigProcessor configProcessor;
        private readonly ErrorPacketToSqlRaceErrorMapper errorMapper;
        private readonly TimeAndSizeWindowBatchProcessor<ErrorPacket> errorProcessor;

        public ErrorDataHandler(
            ISqlRaceWriter sessionWriter,
            SessionConfig sessionConfig,
            ErrorConfigProcessor configProcessor,
            ErrorPacketToSqlRaceErrorMapper errorMapper)
        {
            this.sessionWriter = sessionWriter;
            this.sessionConfig = sessionConfig;
            this.errorPacketQueue = [];
            this.configProcessor = configProcessor;
            this.configProcessor.ProcessErrorComplete += this.OnProcessorProcessErrorComplete;
            this.errorMapper = errorMapper;
            this.errorProcessor = new TimeAndSizeWindowBatchProcessor<ErrorPacket>(this.ProcessPackets, new CancellationTokenSource(), 1000, 1);
        }

        public bool TryHandle(ErrorPacket packet)
        {
            this.errorProcessor.Add(packet);
            return true;
        }

        public void OnProcessorProcessErrorComplete(object? sender, EventArgs e)
        {
            var errorCopy = this.errorPacketQueue.ToArray();
            this.errorPacketQueue.Clear();
            foreach (var packet in errorCopy)
            {
                this.TryHandle(packet);
            }
        }

        private Task ProcessPackets(IReadOnlyList<ErrorPacket> packets)
        {
            foreach (var packet in packets)
            {
                this.Update();
                if (!this.sessionConfig.IsErrorExistInConfig(packet.Name))
                {
                    this.configProcessor.AddErrorToConfig(packet);
                    this.errorPacketQueue.Enqueue(packet);
                    continue;
                }

                var mappedError = this.errorMapper.MapError(packet);
                if (this.sessionWriter.TryWrite(mappedError))
                {
                    continue;
                }

                this.errorPacketQueue.Enqueue(packet);
            }

            return Task.CompletedTask;
        }
    }
}