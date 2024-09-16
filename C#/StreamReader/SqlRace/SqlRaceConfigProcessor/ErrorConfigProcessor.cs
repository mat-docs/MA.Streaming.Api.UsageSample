// <copyright file="ErrorConfigProcessor.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using System.Collections.Concurrent;
using System.Diagnostics;

using MA.Streaming.Core;
using MA.Streaming.OpenData;

using MESL.SqlRace.Domain;
using MESL.SqlRace.Enumerators;

using DataType = MESL.SqlRace.Enumerators.DataType;

namespace Stream.Api.Stream.Reader.SqlRace.SqlRaceConfigProcessor
{
    internal class ErrorConfigProcessor : BaseConfigProcessor
    {
        private readonly TimeAndSizeWindowBatchProcessor<ErrorPacket> errorConfigProcessor;
        private readonly ConcurrentBag<string> errorsProcessed;

        public ErrorConfigProcessor(
            ConfigurationSetManager configurationSetManager,
            RationalConversion defaultConversion,
            IClientSession clientSession,
            ReaderWriterLockSlim configLock,
            SessionConfig sessionConfig)
            : base(
                configurationSetManager,
                defaultConversion,
                clientSession,
                configLock,
                sessionConfig)
        {
            this.errorConfigProcessor =
                new TimeAndSizeWindowBatchProcessor<ErrorPacket>(
                    this.ProcessErrorConfig,
                    new CancellationTokenSource(),
                    100000,
                    10000);
            this.errorsProcessed = [];
        }

        public event EventHandler? ProcessErrorComplete;

        public void AddErrorToConfig(ErrorPacket packet)
        {
            if (this.errorsProcessed.Contains(packet.Name))
            {
                return;
            }

            this.errorsProcessed.Add(packet.Name);
            this.errorConfigProcessor.Add(packet);
        }

        private Task ProcessErrorConfig(IReadOnlyList<ErrorPacket> errorPackets)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            Console.WriteLine($"Adding config for {errorPackets.Count} errors.");
            var configSetIdentifier = Guid.NewGuid().ToString();
            var config = this.ConfigurationSetManager.Create(this.ClientSession.Session.ConnectionString, configSetIdentifier, "");

            config.AddConversion(this.DefaultConversion);

            var channelsToAdd = new Dictionary<string, uint>();
            foreach (var errorPacket in errorPackets)
            {
                var parameterGroup = new ParameterGroup(errorPacket.ApplicationName);
                config.AddParameterGroup(parameterGroup);

                var applicationGroup =
                    new ApplicationGroup(
                        errorPacket.ApplicationName,
                        errorPacket.ApplicationName,
                        [parameterGroup.Identifier])
                    {
                        SupportsRda = false
                    };
                config.AddGroup(applicationGroup);
                var parametersForError = new List<string>();
                for (var i = 0; i < 2; i++)
                {
                    var parameterIdentifier =
                        i == 0 ? $"Current{errorPacket.ErrorIdentifier}" : $"Logged{errorPacket.ErrorIdentifier}";
                    var channelId = this.GenerateUniqueChannelId();
                    channelsToAdd[parameterIdentifier] = channelId;
                    var parameterChannel = new Channel(
                        channelId,
                        parameterIdentifier,
                        0,
                        DataType.Unsigned16Bit,
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
                        [applicationGroup.Name],
                        [channelId],
                        applicationGroup.Name
                    );
                    config.AddParameter(parameter);
                    parametersForError.Add(parameterIdentifier);
                }

                var errorDefinition = new ErrorDefinition(
                    errorPacket.Name,
                    errorPacket.ApplicationName,
                    parametersForError[0],
                    parametersForError[1],
                    errorPacket.Description,
                    // This is the index and not the mask!
                    0);
                config.AddErrorDefinition(errorDefinition);
                this.SessionConfig.SetErrorDefinition(errorPacket.Name, errorDefinition);
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
                Console.WriteLine($"Unable to write config {config.Identifier} due to {ex.Message}");
                return Task.CompletedTask;
            }
            finally
            {
                this.ConfigLock.ExitWriteLock();
            }

            this.ClientSession.Session.UseLoggingConfigurationSet(config.Identifier);

            foreach (var parameter in channelsToAdd)
            {
                this.SessionConfig.SetParameterChannelId(parameter.Key, parameter.Value);
            }

            stopwatch.Stop();
            Console.WriteLine(
                $"Successfully added configuration {config.Identifier} for {errorPackets.Count} errors. Time taken: {stopwatch.ElapsedMilliseconds} ms.");
            this.ProcessErrorComplete?.Invoke(this, EventArgs.Empty);

            return Task.CompletedTask;
        }
    }
}