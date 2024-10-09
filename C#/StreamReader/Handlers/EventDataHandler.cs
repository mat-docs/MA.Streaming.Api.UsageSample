// <copyright file="EventDataHandler.cs" company="McLaren Applied Ltd.">
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
    internal class EventDataHandler : BaseHandler
    {
        private readonly ConcurrentDictionary<ulong, string> eventIdentifierDataFormatCache = new();
        private readonly ISqlRaceWriter sessionWriter;
        private readonly StreamApiClient streamApiClient;
        private readonly SessionConfig sessionConfig;
        private readonly ConcurrentQueue<EventPacket> eventPacketQueue;
        private readonly EventConfigProcessor configProcessor;
        private readonly EventPacketToSqlRaceEventMapper eventMapper;
        private readonly TimeAndSizeWindowBatchProcessor<EventPacket> eventProcessor;

        public EventDataHandler(
            ISqlRaceWriter sessionWriter,
            StreamApiClient streamApiClient,
            SessionConfig sessionConfig,
            EventConfigProcessor configProcessor,
            EventPacketToSqlRaceEventMapper eventMapper)
        {
            this.sessionWriter = sessionWriter;
            this.streamApiClient = streamApiClient;
            this.sessionConfig = sessionConfig;
            this.eventPacketQueue = [];
            this.configProcessor = configProcessor;
            this.configProcessor.ProcessEventComplete += this.OnProcessorProcessEventComplete;
            this.eventMapper = eventMapper;
            this.eventProcessor = new TimeAndSizeWindowBatchProcessor<EventPacket>(this.ProcessPackets, new CancellationTokenSource(), 1000, 1);
        }

        public bool TryHandle(EventPacket packet)
        {
            this.eventProcessor.Add(packet);
            return true;
        }

        private string GetEventIdentifier(ulong dataFormatId)
        {
            if (this.eventIdentifierDataFormatCache.TryGetValue(dataFormatId, out var eventIdentifier))
            {
                return eventIdentifier;
            }

            eventIdentifier = this.streamApiClient.GetEventId(dataFormatId);
            this.eventIdentifierDataFormatCache[dataFormatId] = eventIdentifier;

            return eventIdentifier;
        }

        private void OnProcessorProcessEventComplete(object? sender, EventArgs e)
        {
            var dataQueue = this.eventPacketQueue.ToArray();
            this.eventPacketQueue.Clear();
            foreach (var packet in dataQueue)
            {
                this.TryHandle(packet);
            }
        }

        private Task ProcessPackets(IReadOnlyList<EventPacket> packets)
        {
            foreach (var packet in packets)
            {
                this.Update();
                var eventIdentifier = packet.DataFormat.HasDataFormatIdentifier
                    ? this.GetEventIdentifier(packet.DataFormat.DataFormatIdentifier)
                    : packet.DataFormat.EventIdentifier;

                if (!this.sessionConfig.IsEventExistInConfig(eventIdentifier))
                {
                    this.configProcessor.AddEventToConfig(eventIdentifier);
                    this.eventPacketQueue.Enqueue(packet);
                    continue;
                }

                var mappedEvent = this.eventMapper.MapEvent(packet, eventIdentifier);
                if (this.sessionWriter.TryWrite(mappedEvent))
                {
                    continue;
                }

                this.eventPacketQueue.Enqueue(packet);
            }
            return Task.CompletedTask;
        }
    }
}