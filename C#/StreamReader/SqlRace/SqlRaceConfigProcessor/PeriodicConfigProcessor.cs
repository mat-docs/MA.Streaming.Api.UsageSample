// <copyright file="PeriodicConfigProcessor.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using System.Collections.Concurrent;
using System.Diagnostics;

using MA.Streaming.Core;

using MESL.SqlRace.Domain;
using MESL.SqlRace.Enumerators;

namespace Stream.Api.Stream.Reader.SqlRace.SqlRaceConfigProcessor
{
    internal class PeriodicConfigProcessor : BaseConfigProcessor<Tuple<string, uint>>
    {
        private readonly TimeAndSizeWindowBatchProcessor<Tuple<string, uint>> periodicConfigProcessor;
        private readonly ConcurrentBag<string> parametersProcessed;

        public PeriodicConfigProcessor(
            ConfigurationSetManager configSetManager,
            RationalConversion defaultConversion,
            IClientSession clientSession,
            ReaderWriterLockSlim configLock,
            SessionConfig sessionConfig)
            : base(configSetManager, defaultConversion, clientSession, configLock, sessionConfig)
        {
            this.periodicConfigProcessor =
                new TimeAndSizeWindowBatchProcessor<Tuple<string, uint>>(
                    this.ProcessPeriodicConfig,
                    new CancellationTokenSource(),
                    100000,
                    10000);
            this.parametersProcessed = [];
        }

        public override event EventHandler? ProcessCompleted;

        public override void AddToConfig(Tuple<string, uint> parameter)
        {
            if (this.parametersProcessed.Contains(parameter.Item1 + parameter.Item2))
            {
                return;
            }

            this.parametersProcessed.Add(parameter.Item1 + parameter.Item2);
            this.periodicConfigProcessor.Add(parameter);
        }

        private Task ProcessPeriodicConfig(IReadOnlyList<Tuple<string, uint>> parameterIdentifiers)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            Console.WriteLine($"Adding config for {parameterIdentifiers.Count} periodic parameters.");

            var configSetIdentifier = Guid.NewGuid().ToString();
            var config = this.ConfigurationSetManager.Create(this.ClientSession.Session.ConnectionString, configSetIdentifier, "");
            config.AddConversion(this.DefaultConversion);

            var channelsToAdd = new Dictionary<string, Tuple<uint, uint>>();
            try
            {
                foreach (var parameter in parameterIdentifiers)
                {
                    var parameterGroup = new ParameterGroup(parameter.Item1.Split(':')[1]);
                    config.AddParameterGroup(parameterGroup);
                    var applicationGroup =
                        new ApplicationGroup(
                            parameter.Item1.Split(':')[1],
                            parameter.Item1.Split(':')[1],
                            new List<string>
                            {
                                parameterGroup.Identifier
                            })
                        {
                            SupportsRda = false
                        };
                    config.AddGroup(applicationGroup);

                    var channelId = this.GenerateUniqueChannelId();
                    channelsToAdd[parameter.Item1] = new Tuple<uint, uint>(parameter.Item2, channelId);
                    var parameterChannel = new Channel(
                        channelId,
                        parameter.Item1,
                        parameter.Item2,
                        DataType.Double64Bit,
                        ChannelDataSourceType.Periodic,
                        parameter.Item1);
                    config.AddChannel(parameterChannel);
                    var parameterObj = new Parameter(
                        parameter.Item1,
                        parameter.Item1.Split(':')[0],
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
                    config.AddParameter(parameterObj);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to create config {configSetIdentifier} for {parameterIdentifiers.Count} periodic parameters due to {ex.Message}.");
                return Task.CompletedTask;
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
                this.SessionConfig.SetParameterChannelId(parameter.Key, parameter.Value.Item1, parameter.Value.Item2);
            }

            stopwatch.Stop();
            Console.WriteLine(
                $"Successfully added configuration {config.Identifier} for {parameterIdentifiers.Count} periodic parameters. Time Taken: {stopwatch.ElapsedMilliseconds} ms.");
            this.ProcessCompleted?.Invoke(this, EventArgs.Empty);

            return Task.CompletedTask;
        }
    }
}