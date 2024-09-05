// <copyright file="ConfigurationProcessor.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using System.Collections.Concurrent;
using MA.Streaming.Core;
using MESL.SqlRace.Domain;

namespace Stream.Api.Stream.Reader.SqlRace
{
    internal class ConfigurationProcessor
    {
        private readonly IClientSession clientSession;
        private readonly TimeAndSizeWindowBatchProcessor<List<string>> rowConfigProcessor;
        private readonly TimeAndSizeWindowBatchProcessor<string> eventConfigProcessor;
        private readonly ConcurrentBag<string> eventsAndParametersProcessed = new();
        private readonly TimeAndSizeWindowBatchProcessor<Tuple<string, uint>> periodicConfigProcessor;
        private readonly AtlasSessionWriter writer;

        public ConfigurationProcessor(AtlasSessionWriter writer, IClientSession clientSession)
        {
            this.writer = writer;
            this.clientSession = clientSession;
            this.rowConfigProcessor =
                new TimeAndSizeWindowBatchProcessor<List<string>>(
                    this.ProcessRowConfig, new CancellationTokenSource(), 100000,
                    10000);
            this.eventConfigProcessor =
                new TimeAndSizeWindowBatchProcessor<string>(
                    this.ProcessEventConfig, new CancellationTokenSource(), 100000,
                    10000);
            this.periodicConfigProcessor =
                new TimeAndSizeWindowBatchProcessor<Tuple<string, uint>>(
                    this.ProcessPeriodicConfig,
                    new CancellationTokenSource(), 100000, 10000);
        }

        public event EventHandler? ProcessRowComplete;
        public event EventHandler? ProcessEventComplete;
        public event EventHandler? ProcessPeriodicComplete;

        public void AddRowPacketParameter(List<string> parameters)
        {
            var newParameters = parameters.Where(x => !this.eventsAndParametersProcessed.Contains(x)).ToList();
            if (!newParameters.Any()) return;

            foreach (var parameter in newParameters) this.eventsAndParametersProcessed.Add(parameter);

            this.rowConfigProcessor.Add(newParameters);
        }

        public void AddPeriodicPacketParameter(Tuple<string, uint> parameter)
        {
            if (this.eventsAndParametersProcessed.Contains(parameter.Item1 + parameter.Item2)) return;
            this.eventsAndParametersProcessed.Add(parameter.Item1 + parameter.Item2);
            this.periodicConfigProcessor.Add(parameter);
        }

        public void AddPacketEvent(string eventIdentifier)
        {
            if (this.eventsAndParametersProcessed.Contains(eventIdentifier)) return;
            this.eventsAndParametersProcessed.Add(eventIdentifier);
            this.eventConfigProcessor.Add(eventIdentifier);
        }

        private Task ProcessPeriodicConfig(IReadOnlyList<Tuple<string, uint>> parameters)
        {
            this.writer.AddBasicPeriodicParameterConfiguration(this.clientSession, parameters);
            this.ProcessPeriodicComplete?.Invoke(this, null);
            return Task.CompletedTask;
        }

        private Task ProcessRowConfig(IReadOnlyList<List<string>> parameters)
        {
            this.writer.AddBasicRowParameterConfiguration(this.clientSession, parameters.SelectMany(i => i).ToList());
            this.ProcessRowComplete?.Invoke(this, null);
            return Task.CompletedTask;
        }

        private Task ProcessEventConfig(IReadOnlyList<string> eventIdentifiers)
        {
            this.writer.AddBasicEventConfiguration(this.clientSession, eventIdentifiers);
            this.ProcessEventComplete?.Invoke(this, null);
            return Task.CompletedTask;
        }
    }
}