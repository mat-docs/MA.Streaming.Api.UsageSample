// <copyright file="ConfigurationProcessor.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using System.Collections.Concurrent;
using MA.Streaming.Core;
using MESL.SqlRace.Domain;

namespace Stream.Api.Stream.Reader.SqlRace
{
    internal class ConfigurationProcessor
    {
        private readonly IClientSession _clientSession;
        private readonly TimeAndSizeWindowBatchProcessor<List<string>> _rowConfigProcessor;
        private readonly TimeAndSizeWindowBatchProcessor<string> _eventConfigProcessor;
        private readonly ConcurrentBag<string> _eventsAndParametersProcessed = new();
        private readonly TimeAndSizeWindowBatchProcessor<Tuple<string, uint>> _periodicConfigProcessor;
        private readonly AtlasSessionWriter _writer;

        public ConfigurationProcessor(AtlasSessionWriter writer, IClientSession clientSession)
        {
            _writer = writer;
            _clientSession = clientSession;
            _rowConfigProcessor =
                new TimeAndSizeWindowBatchProcessor<List<string>>(ProcessRowConfig, new CancellationTokenSource(), 100000,
                    10000);
            _eventConfigProcessor =
                new TimeAndSizeWindowBatchProcessor<string>(ProcessEventConfig, new CancellationTokenSource(), 100000,
                    10000);
            _periodicConfigProcessor =
                new TimeAndSizeWindowBatchProcessor<Tuple<string, uint>>(ProcessPeriodicConfig,
                    new CancellationTokenSource(), 100000, 10000);
        }

        public event EventHandler? ProcessRowComplete;
        public event EventHandler? ProcessEventComplete;
        public event EventHandler? ProcessPeriodicComplete;

        public void AddRowPacketParameter(List<string> parameters)
        {
            var newParameters = parameters.Where(x => !_eventsAndParametersProcessed.Contains(x)).ToList();
            if (!newParameters.Any()) return;

            foreach (var parameter in newParameters) _eventsAndParametersProcessed.Add(parameter);

            _rowConfigProcessor.Add(newParameters);
        }

        public void AddPeriodicPacketParameter(Tuple<string, uint> parameter)
        {
            if (_eventsAndParametersProcessed.Contains(parameter.Item1 + "Periodic" + parameter.Item2)) return;
            _eventsAndParametersProcessed.Add(parameter.Item1 + "Periodic" + parameter.Item2);
            _periodicConfigProcessor.Add(parameter);
        }

        public void AddPacketEvent(string eventIdentifier)
        {
            if (_eventsAndParametersProcessed.Contains(eventIdentifier)) return;
            _eventsAndParametersProcessed.Add(eventIdentifier);
            _eventConfigProcessor.Add(eventIdentifier);
        }

        private Task ProcessPeriodicConfig(IReadOnlyList<Tuple<string, uint>> parameters)
        {
            _writer.AddBasicPeriodicParameterConfiguration(_clientSession, parameters);
            ProcessPeriodicComplete?.Invoke(this, null);
            return Task.CompletedTask;
        }

        private Task ProcessRowConfig(IReadOnlyList<List<string>> parameters)
        {
            _writer.AddBasicRowParameterConfiguration(_clientSession, parameters.SelectMany(i => i).ToList());
            ProcessRowComplete?.Invoke(this, null);
            return Task.CompletedTask;
        }

        private Task ProcessEventConfig(IReadOnlyList<string> eventIdentifiers)
        {
            _writer.AddBasicEventConfiguration(_clientSession, eventIdentifiers);
            ProcessEventComplete?.Invoke(this, null);
            return Task.CompletedTask;
        }
    }
}