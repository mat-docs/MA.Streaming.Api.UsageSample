// <copyright file="SynchroConfigProcessor.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using System.Collections.Concurrent;
using System.Diagnostics;

using MA.Streaming.Core;

using MESL.SqlRace.Domain;
using MESL.SqlRace.Enumerators;

namespace Stream.Api.Stream.Reader.SqlRace.SqlRaceConfigProcessor
{
    internal class SynchroConfigProcessor : BaseConfigProcessor
    {
        private readonly TimeAndSizeWindowBatchProcessor<string> synchroConfigProcessor;
        private readonly ConcurrentBag<string> parametersAlreadyProcessed;

        public SynchroConfigProcessor(
            ConfigurationSetManager configurationSetManager,
            RationalConversion defaultConversion,
            IClientSession clientSession,
            ReaderWriterLockSlim configLock,
            SessionConfig sessionConfig)
            : base(configurationSetManager, defaultConversion, clientSession, configLock, sessionConfig)
        {
            this.parametersAlreadyProcessed = [];
            this.synchroConfigProcessor = new TimeAndSizeWindowBatchProcessor<string>(this.ConfigProcessor, new CancellationTokenSource(), 1000, 100);
        }

        public event EventHandler? ProcessSynchroComplete;

        public void AddSynchroParameterToConfig(IReadOnlyList<string> parameterList)
        {
            var newParameters = parameterList.Where(x => !this.parametersAlreadyProcessed.Contains(x)).ToList();
            if (!newParameters.Any())
            {
                return;
            }

            newParameters.ForEach(
                x =>
                {
                    this.synchroConfigProcessor.Add(x);
                    this.parametersAlreadyProcessed.Add(x);
                });
        }

        private Task ConfigProcessor(IReadOnlyList<string> parameterList)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            Console.WriteLine($"Adding config for {parameterList.Count} synchro parameters.");

            var configSetIdentifier = Guid.NewGuid().ToString();
            var config = this.ConfigurationSetManager.Create(this.ClientSession.Session.ConnectionString, configSetIdentifier, "");
            config.AddConversion(this.DefaultConversion);

            var channelsToAdd = new Dictionary<string, uint>();
            foreach (var parameter in parameterList)
            {
                var parameterName = parameter.Split(':')[0];
                var appName = parameter.Split(":")[1];
                var parameterGroup = new ParameterGroup(appName);
                config.AddParameterGroup(parameterGroup);
                var applicationGroup =
                    new ApplicationGroup(
                        appName,
                        appName,
                        [parameterGroup.Identifier])
                    {
                        SupportsRda = false
                    };
                config.AddGroup(applicationGroup);
                var channelId = this.GenerateUniqueChannelId();
                channelsToAdd[parameter] = channelId;
                var parameterChannel = new Channel(
                    channelId,
                    parameter,
                    0,
                    DataType.Double64Bit,
                    ChannelDataSourceType.Synchro,
                    parameter);
                config.AddChannel(parameterChannel);
                var parameterObj = new Parameter(
                    parameter,
                    parameterName,
                    "",
                    0,
                    100,
                    0,
                    100,
                    0.0,
                    0xFFFF,
                    0,
                    "DefaultConversion",
                    [applicationGroup.Name],
                    [channelId],
                    applicationGroup.Name
                );
                config.AddParameter(parameterObj);
            }

            Console.WriteLine($"Commiting config {config.Identifier}.");
            try
            {
                this.ConfigLock.EnterWriteLock();
                config.Commit();
            }
            catch (ConfigurationSetAlreadyExistsException)
            {
                Console.WriteLine($"Config {config.Identifier} already exists.");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to add Config {config.Identifier} due to {ex.Message}");
                return Task.CompletedTask;
            }
            finally
            {
                this.ConfigLock.ExitWriteLock();
            }

            this.ClientSession.Session.UseLoggingConfigurationSet(config.Identifier);

            foreach (var parameter in channelsToAdd)
            {
                this.SessionConfig.SetSynchroChannelId(parameter.Key, parameter.Value);
            }

            stopwatch.Stop();
            Console.WriteLine(
                $"Successfully added configuration {config.Identifier} for {parameterList.Count} synchro parameters. Time Taken: {stopwatch.ElapsedMilliseconds} ms.");
            this.ProcessSynchroComplete?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
    }
}