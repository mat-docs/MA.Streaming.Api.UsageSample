// <copyright file="ErrorDataHandler.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using System.Collections.Concurrent;

using MA.DataPlatforms.DataRecorder.SqlRaceWriter.Abstractions;
using MA.Streaming.Core;
using MA.Streaming.OpenData;

using Stream.Api.Stream.Reader.Abstractions;
using Stream.Api.Stream.Reader.SqlRace;
using Stream.Api.Stream.Reader.SqlRace.Mappers;

namespace Stream.Api.Stream.Reader.Handlers
{
    internal class ErrorDataHandler : BaseHandler<ErrorPacket>
    {
        private readonly ISqlRaceWriter sessionWriter;
        private readonly SessionConfig sessionConfig;
        private readonly ConcurrentQueue<ErrorPacket> errorPacketQueue;
        private readonly IConfigProcessor<ErrorPacket> configProcessor;
        private readonly ErrorPacketToSqlRaceErrorMapper errorMapper;
        private readonly TimeAndSizeWindowBatchProcessor<ErrorPacket> errorProcessor;

        public ErrorDataHandler(
            ISqlRaceWriter sessionWriter,
            SessionConfig sessionConfig,
            IConfigProcessor<ErrorPacket> configProcessor,
            ErrorPacketToSqlRaceErrorMapper errorMapper)
        {
            this.sessionWriter = sessionWriter;
            this.sessionConfig = sessionConfig;
            this.errorPacketQueue = [];
            this.configProcessor = configProcessor;
            this.configProcessor.ProcessCompleted += this.OnProcessorProcessErrorComplete;
            this.errorMapper = errorMapper;
            this.errorProcessor = new TimeAndSizeWindowBatchProcessor<ErrorPacket>(this.ProcessPackets, new CancellationTokenSource(), 1000, 1);
        }

        public override void Handle(ErrorPacket packet)
        {
            this.errorProcessor.Add(packet);
        }

        public void OnProcessorProcessErrorComplete(object? sender, EventArgs e)
        {
            var errorCopy = this.errorPacketQueue.ToArray();
            this.errorPacketQueue.Clear();
            foreach (var packet in errorCopy)
            {
                this.Handle(packet);
            }
        }

        private Task ProcessPackets(IReadOnlyList<ErrorPacket> packets)
        {
            foreach (var packet in packets)
            {
                this.Update();
                if (!this.sessionConfig.IsErrorExistInConfig(packet.Name))
                {
                    this.configProcessor.AddToConfig(packet);
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