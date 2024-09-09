﻿// <copyright file="RowConfigProcessor.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using System.Collections.Concurrent;
using System.Diagnostics;

using MA.Streaming.Core;

using MESL.SqlRace.Domain;
using MESL.SqlRace.Enumerators;

namespace Stream.Api.Stream.Reader.SqlRace.SqlRaceConfigProcessor
{
    internal class RowConfigProcessor : BaseConfigProcessor
    {
        private readonly TimeAndSizeWindowBatchProcessor<List<string>> rowConfigProcessor;
        private readonly ConcurrentBag<string> parametersProcessed;

        public RowConfigProcessor(
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
            this.rowConfigProcessor =
                new TimeAndSizeWindowBatchProcessor<List<string>>(
                    this.ProcessRowConfig,
                    new CancellationTokenSource(),
                    100000,
                    10000);
            this.parametersProcessed = [];
        }

        public event EventHandler? ProcessRowComplete;

        public void AddParameterToConfig(List<string> parameterList)
        {
            var newParameters = parameterList.Where(x => !this.parametersProcessed.Contains(x)).ToList();
            if (!newParameters.Any())
            {
                return;
            }

            foreach (var parameter in newParameters)
            {
                this.parametersProcessed.Add(parameter);
            }

            this.rowConfigProcessor.Add(newParameters);
        }

        private Task ProcessRowConfig(IReadOnlyList<List<string>> parameterIdentifiers)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            Console.WriteLine($"Adding config for {parameterIdentifiers.Count} row parameters.");
            var parameterIdentifiersToProcess = parameterIdentifiers.SelectMany(x => x).ToList();
            var configSetIdentifier = Guid.NewGuid().ToString();
            var config = this.ConfigurationSetManager.Create(this.ClientSession.Session.ConnectionString, configSetIdentifier, "");

            config.AddConversion(this.DefaultConversion);

            var channelsToAdd = new Dictionary<string, uint>();
            foreach (var parameterIdentifier in parameterIdentifiersToProcess)
            {
                var parameterGroup = new ParameterGroup(parameterIdentifier.Split(':')[1]);
                config.AddParameterGroup(parameterGroup);

                var applicationGroup =
                    new ApplicationGroup(
                        parameterIdentifier.Split(':')[1],
                        parameterIdentifier.Split(':')[1],
                        new List<string>
                        {
                            parameterGroup.Identifier
                        })
                    {
                        SupportsRda = false
                    };
                config.AddGroup(applicationGroup);

                var channelId = this.GenerateUniqueChannelId();
                channelsToAdd[parameterIdentifier] = channelId;
                var parameterChannel = new Channel(
                    channelId,
                    parameterIdentifier,
                    0,
                    DataType.Double64Bit,
                    ChannelDataSourceType.RowData,
                    parameterIdentifier);
                config.AddChannel(parameterChannel);
                var parameter = new Parameter(
                    parameterIdentifier,
                    parameterIdentifier.Split(':')[0],
                    "",
                    0,
                    100,
                    0,
                    100,
                    0.0,
                    0xFFFF,
                    0,
                    "DefaultConversion",
                    new List<string>
                    {
                        applicationGroup.Name
                    },
                    new List<uint>
                    {
                        channelId
                    },
                    applicationGroup.Name
                );
                config.AddParameter(parameter);
            }

            Console.WriteLine($"Commiting config {config.Identifier}.");
            try
            {
                lock (this.ConfigLock)
                {
                    config.Commit();
                }

                this.ClientSession.Session.UseLoggingConfigurationSet(config.Identifier);

                foreach (var parameter in channelsToAdd)
                {
                    this.SessionConfig.SetParameterChannelId(parameter.Key, parameter.Value);
                }

                stopwatch.Stop();
                Console.WriteLine(
                    $"Successfully added configuration {config.Identifier} for {parameterIdentifiers.Count} row parameters. Time taken: {stopwatch.ElapsedMilliseconds} ms.");
                this.ProcessRowComplete?.Invoke(this, EventArgs.Empty);
            }
            catch (ConfigurationSetAlreadyExistsException)
            {
                Console.WriteLine($"Config {config.Identifier} already exists.");
            }

            return Task.CompletedTask;
        }
    }
}