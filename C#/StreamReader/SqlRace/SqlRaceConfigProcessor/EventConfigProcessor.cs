// <copyright file="EventConfigProcessor.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;

using MA.Streaming.Core;
using MA.Streaming.OpenData;

using MESL.SqlRace.Domain;
using MESL.SqlRace.Enumerators;

using EventDefinition = MESL.SqlRace.Domain.EventDefinition;

namespace Stream.Api.Stream.Reader.SqlRace.SqlRaceConfigProcessor
{
    internal class EventConfigProcessor : BaseConfigProcessor
    {
        private readonly Dictionary<EventPriority, EventPriorityType> eventPriorityDictionary =
            new()
            {
                {
                    EventPriority.Critical, EventPriorityType.High
                },
                {
                    EventPriority.High, EventPriorityType.High
                },
                {
                    EventPriority.Medium, EventPriorityType.Medium
                },
                {
                    EventPriority.Low, EventPriorityType.Low
                },
                {
                    EventPriority.Debug, EventPriorityType.Debug
                },
                {
                    EventPriority.Unspecified, EventPriorityType.Low
                }
            };
        private readonly TimeAndSizeWindowBatchProcessor<string> eventConfigProcessor;
        private readonly ConcurrentBag<string> eventsProcessed;

        public EventConfigProcessor(
            ConfigurationSetManager configurationSetManager,
            RationalConversion defaultConversion,
            IClientSession clientSession,
            object configLock,
            SessionConfig sessionConfig)
            : base(
                configurationSetManager,
                defaultConversion,
                clientSession,
                configLock,
                sessionConfig)
        {
            this.eventConfigProcessor =
                new TimeAndSizeWindowBatchProcessor<string>(
                    this.ProcessEventConfig,
                    new CancellationTokenSource(),
                    100000,
                    10000);
            this.eventsProcessed = [];
        }

        public event EventHandler? ProcessEventComplete;

        public void AddEventToConfig(string eventIdentifier)
        {
            if (this.eventsProcessed.Contains(eventIdentifier))
            {
                return;
            }

            this.eventsProcessed.Add(eventIdentifier);
            this.eventConfigProcessor.Add(eventIdentifier);
        }

        private Task ProcessEventConfig(IReadOnlyList<string> eventIdentifiers)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            Console.WriteLine($"Adding config for {eventIdentifiers.Count} events.");
            var configSetIdentifier = Guid.NewGuid().ToString();
            var config = this.ConfigurationSetManager.Create(this.ClientSession.Session.ConnectionString, configSetIdentifier, "");
            config.AddConversion(this.DefaultConversion);

            var eventsToAdd = new Dictionary<string, EventDefinition>();
            foreach (var eventIdentifier in eventIdentifiers)
            {
                var appGroupName = eventIdentifier.Split(':')[1];

                var applicationGroup =
                    new ApplicationGroup(appGroupName, appGroupName)
                    {
                        SupportsRda = false
                    };
                config.AddGroup(applicationGroup);

                var eventDefId = int.Parse(eventIdentifier.Split(':')[0], NumberStyles.AllowHexSpecifier);
                var eventDefinitionSql = new EventDefinition(
                    eventDefId,
                    eventIdentifier,
                    EventPriorityType.Low,
                    new List<string>
                    {
                        "DefaultConversion",
                        "DefaultConversion",
                        "DefaultConversion"
                    },
                    appGroupName
                );
                config.AddEventDefinition(eventDefinitionSql);
                eventsToAdd[eventIdentifier] = eventDefinitionSql;
            }

            Console.WriteLine($"Commiting config {config.Identifier}.");
            try
            {
                lock (this.ConfigLock)
                {
                    config.Commit();
                }

                this.ClientSession.Session.UseLoggingConfigurationSet(config.Identifier);

                foreach (var events in eventsToAdd)
                {
                    this.SessionConfig.SetEventDefinition(events.Key, events.Value);
                }

                stopwatch.Stop();
                Console.WriteLine(
                    $"Successfully added configuration {config.Identifier} for {eventIdentifiers.Count} events. Time Taken: {stopwatch.ElapsedMilliseconds}.");
                this.ProcessEventComplete?.Invoke(this, EventArgs.Empty);
            }
            catch (ConfigurationSetAlreadyExistsException)
            {
                Console.WriteLine($"Config {config.Identifier} already exists.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to add config due to {ex.Message}");
            }

            return Task.CompletedTask;
        }
    }
}