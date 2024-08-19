// <copyright file="ConfigurationProcessor.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using System.Collections.Concurrent;
using MA.Streaming.Core;
using MESL.SqlRace.Domain;

namespace Stream.Api.Stream.Reader
{
    internal class ConfigurationProcessor
    {
        private readonly IClientSession clientSession;
        private readonly TimeAndSizeWindowBatchProcessor<List<string>> configProcessor;
        private readonly TimeAndSizeWindowBatchProcessor<string> eventConfigProcessor;
        private readonly ConcurrentBag<string> eventsAndParametersProcessed = new();
        private readonly TimeAndSizeWindowBatchProcessor<Tuple<string, uint>> periodicConfigProcessor;
        private readonly AtlasSessionWriter writer;

        public ConfigurationProcessor(AtlasSessionWriter writer, IClientSession clientSession)
        {
            this.writer = writer;
            this.clientSession = clientSession;
            configProcessor =
                new TimeAndSizeWindowBatchProcessor<List<string>>(ProcessConfig, new CancellationTokenSource(), 100000,
                    10000);
            eventConfigProcessor =
                new TimeAndSizeWindowBatchProcessor<string>(ProcessEventConfig, new CancellationTokenSource(), 100000,
                    10000);
            periodicConfigProcessor =
                new TimeAndSizeWindowBatchProcessor<Tuple<string, uint>>(ProcessPeriodicConfig,
                    new CancellationTokenSource(), 100000, 10000);
        }

        public event EventHandler ProcessComplete;
        public event EventHandler ProcessEventComplete;
        public event EventHandler ProcessPeriodicComplete;

        public void AddPacketParameter(List<string> parameters)
        {
            var newParameters = parameters.Where(x => !eventsAndParametersProcessed.Contains(x)).ToList();
            if (!newParameters.Any()) return;

            foreach (var parameter in newParameters) eventsAndParametersProcessed.Add(parameter);
            ;
            configProcessor.Add(newParameters);
        }

        public void AddPeriodicPacketParameter(Tuple<string, uint> parameter)
        {
            if (eventsAndParametersProcessed.Contains(parameter.Item1 + "Periodic")) return;
            eventsAndParametersProcessed.Add(parameter.Item1 + "Periodic");
            periodicConfigProcessor.Add(parameter);
        }

        public void AddPacketEvent(string eventIdentifier)
        {
            if (eventsAndParametersProcessed.Contains(eventIdentifier)) return;
            eventsAndParametersProcessed.Add(eventIdentifier);
            eventConfigProcessor.Add(eventIdentifier);
        }

        private Task ProcessPeriodicConfig(IReadOnlyList<Tuple<string, uint>> parameters)
        {
            writer.AddBasicPeriodicParameterConfiguration(clientSession, parameters);
            ProcessPeriodicComplete?.Invoke(this, null);
            return Task.CompletedTask;
        }

        private Task ProcessConfig(IReadOnlyList<List<string>> parameters)
        {
            writer.AddBasicParameterConfiguration(clientSession, parameters.SelectMany(i => i).ToList());
            ProcessComplete?.Invoke(this, null);
            return Task.CompletedTask;
        }

        private Task ProcessEventConfig(IReadOnlyList<string> eventIdentifiers)
        {
            writer.AddBasicEventConfiguration(clientSession, eventIdentifiers);
            ProcessEventComplete?.Invoke(this, null);
            return Task.CompletedTask;
        }
    }
}