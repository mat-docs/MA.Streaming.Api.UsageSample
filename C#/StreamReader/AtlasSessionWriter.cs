// <copyright file="AtlasSessionWriter.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using System.Collections.Concurrent;
using System.Net;
using Google.Protobuf.Collections;
using MA.Streaming.API;
using MA.Streaming.OpenData;
using MAT.OCS.Core;
using MESL.SqlRace.Domain;
using MESL.SqlRace.Enumerators;
using DataType = MESL.SqlRace.Enumerators.DataType;
using EventDefinition = MESL.SqlRace.Domain.EventDefinition;

namespace Stream.Api.Stream.Reader
{
    internal class AtlasSessionWriter
    {
        private readonly string connectionString;

        private readonly Dictionary<EventPriority, EventPriorityType> eventPriorityDictionary =
            new()
            {
                { EventPriority.Critical, EventPriorityType.High },
                { EventPriority.High, EventPriorityType.High },
                { EventPriority.Medium, EventPriorityType.Medium },
                { EventPriority.Low, EventPriorityType.Low },
                { EventPriority.Debug, EventPriorityType.Debug },
                { EventPriority.Unspecified, EventPriorityType.Low }
            };

        public ConcurrentDictionary<string, uint> channelIdParameterDictionary = new();
        public ConcurrentDictionary<string, Dictionary<uint, uint>> channelIdPeriodicParameterDictionary = new();

        private readonly object configLock = new();
        private ConfigurationSetManager configSetManager;
        private uint currentChannelId = 0;

        private readonly RationalConversion defaultConversion =
            RationalConversion.CreateSimple1To1Conversion("DefaultConversion", "", "%5.2f");

        public ConcurrentDictionary<string, EventDefinition> eventDefCache = new();

        private SessionManager? sessionManager;
        private int sampleCounter = 0;

        public AtlasSessionWriter(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public void Initialise()
        {
            Console.WriteLine("Initializing");
            if (!Core.IsInitialized)
            {
                Console.WriteLine("Initializing SQL Race.");
                Core.LicenceProgramName = "SQLRace";
                Core.Initialize();
                Console.WriteLine("SQL Race Initialized.");
            }

            sessionManager = SessionManager.CreateSessionManager();
            configSetManager = new ConfigurationSetManager();
        }

        public IClientSession CreateSession(string sessionName, string sessionType)
        {
            StartRecorder();
            var sessionKey = SessionKey.NewKey();
            var sessionDate = DateTime.Now;
            var clientSession = sessionManager.CreateSession(connectionString, sessionKey, sessionName,
                sessionDate, sessionType);
            Console.WriteLine($"New Session is created with name {sessionName}.");
            return clientSession;
        }

        private void StartRecorder(int port = 7300)
        {
            if (!sessionManager.ServerListener.IsRunning)
            {
                Core.ConfigureServer(true, IPEndPoint.Parse($"127.0.0.1:{port}"));
                Console.WriteLine($"Sever Listener is Running on {sessionManager.ServerListener.ServerEndPoint}");
            }
            else
            {
                Console.WriteLine(
                    $"Server listener is already running on {sessionManager.ServerListener.ServerEndPoint}");
            }

            var recorderConfiguration = RecordersConfiguration.GetRecordersConfiguration();
            recorderConfiguration.AddConfiguration(
                Guid.NewGuid(),
                "SQLServer",
                connectionString,
                connectionString,
                connectionString,
                false
            );
        }

        //public void AddConfiguration(IClientSession clientSession, ConfigurationPacket packet)
        //{
        //    Console.WriteLine($"Adding configuration {packet.ConfigId}.");
        //    var configIdentifier = packet.ConfigId;

        //    if (configSetManager.Exists(new DatabaseConnectionInformation(connectionString), configIdentifier))
        //    {
        //        Console.WriteLine($"Configuration {packet.ConfigId} already exists, using existing config.");
        //        clientSession.Session.UseLoggingConfigurationSet(configIdentifier);
        //        return;
        //    }

        //    var config = configSetManager.Create(connectionString, configIdentifier, "");
        //    var defaultConversionFuncName = defaultConversion.Name;
        //    config.AddConversion(defaultConversion);
        //    var apps = packet.ParameterDefinitions.Select(x => x.ApplicationName).ToList();

        //    var appGroups = apps.Select(x =>
        //        new ApplicationGroup(x, CreateParameterGroups(packet.GroupDefinitions, config, x)))
        //        .ToList();

        //    config.AddGroups(appGroups);
        //    var channelIdsToAdd = new Dictionary<string, uint>();
        //    var periodicChannelIdsToAdd = new Dictionary<string, Dictionary<uint, uint>>();

        //    foreach (var parameterDefinition in packet.ParameterDefinitions)
        //    {
        //        var conversionFuncName = defaultConversionFuncName;
        //        var channelId = GenerateUniqueChannelId();
        //        channelIdsToAdd[parameterDefinition.Identifier] = channelId;
        //        var parameterChannel = new Channel(channelId, parameterDefinition.Name, 0, DataType.Double64Bit,
        //            ChannelDataSourceType.RowData, parameterDefinition.Name);
        //        config.AddChannel(parameterChannel);
        //        if (parameterDefinition.Conversion != null)
        //        {
        //            var inputValues = new double[parameterDefinition.Conversion.InputValues.Count];
        //            var stringValues = new string[parameterDefinition.Conversion.StringValues.Count];
        //            parameterDefinition.Conversion.InputValues.CopyTo(inputValues, 0);
        //            parameterDefinition.Conversion.StringValues.CopyTo(stringValues, 0);
        //            config.AddConversion(new TextConversion(
        //                parameterDefinition.Conversion.ConversionIdentifier,
        //                parameterDefinition.Units,
        //                parameterDefinition.FormatString,
        //                inputValues,
        //                stringValues,
        //                parameterDefinition.Conversion.Default
        //            ));
        //            conversionFuncName = parameterDefinition.Conversion.ConversionIdentifier;
        //        }

        //        var parameter = new Parameter(
        //            parameterDefinition.Identifier,
        //            parameterDefinition.Name,
        //            parameterDefinition.Description,
        //            parameterDefinition.MaxValue,
        //            parameterDefinition.MinValue,
        //            parameterDefinition.WarningMaxValue,
        //            parameterDefinition.WarningMinValue,
        //            0.0,
        //            0xFFFF,
        //            0,
        //            conversionFuncName,
        //            new List<string> { parameterDefinition.ApplicationName },
        //            new List<uint> { channelId },
        //            parameterDefinition.ApplicationName,
        //            parameterDefinition.FormatString,
        //            parameterDefinition.Units
        //        );
        //        config.AddParameter(parameter);
        //    }

        //    foreach (var eventDefinition in packet.EventDefinitions)
        //    {
        //        var conversionFuncNames = new string[]
        //            { defaultConversionFuncName, defaultConversionFuncName, defaultConversionFuncName };
        //        for (var i = 0; i < eventDefinition.Conversions.Count; i++)
        //            if (eventDefinition.Conversions[i].ConversionIdentifier != "")
        //            {
        //                var inputValues = new double[eventDefinition.Conversions[i].InputValues.Count];
        //                var stringValues = new string[eventDefinition.Conversions[i].StringValues.Count];
        //                eventDefinition.Conversions[i].InputValues.CopyTo(inputValues, 0);
        //                eventDefinition.Conversions[i].StringValues.CopyTo(stringValues, 0);
        //                config.AddConversion(new TextConversion(
        //                    eventDefinition.Conversions[i].ConversionIdentifier,
        //                    "",
        //                    "5.2f",
        //                    inputValues,
        //                    stringValues,
        //                    eventDefinition.Conversions[i].Default
        //                ));
        //                conversionFuncNames[i] = eventDefinition.Conversions[i].ConversionIdentifier;
        //            }

        //        var eventDefId = (int)eventDefinition.DefinitionId;
        //        var eventDefinitionSql = new EventDefinition(
        //            eventDefId,
        //            eventDefinition.Description,
        //            eventPriorityDictionary[eventDefinition.Priority],
        //            conversionFuncNames,
        //            eventDefinition.ApplicationName
        //        );
        //        config.AddEventDefinition(eventDefinitionSql);
        //    }

        //    Console.WriteLine($"Commiting config {configIdentifier}.");
        //    try
        //    {
        //        lock (configLock)
        //        {
        //            config.Commit();
        //            clientSession.Session.UseLoggingConfigurationSet(config.Identifier);
        //        }

        //        Console.WriteLine($"Successfully added configuration {configIdentifier}");
        //    }
        //    catch (ConfigurationSetAlreadyExistsException)
        //    {
        //        Console.WriteLine($"Config {configIdentifier} already exists.");
        //    }
        //}

        private List<string> CreateParameterGroups(IEnumerable<GroupDefinition> groupDefinitions, ConfigurationSet config, string appName)
        {
            var groups = groupDefinitions
                .Select(x => x)
                .Where(x => x.Equals(appName))
                .ToList();

            var parameterGroups = new List<ParameterGroup>();

            foreach (var group in groups)
            {
                var subGroups = new List<string>();
                if (group.Groups.Any())
                {
                    subGroups = CreateParameterGroups(group.Groups, config, appName);
                }
                parameterGroups.Add(new ParameterGroup(group.Identifier, group.Description, subGroups));
            }
            config.AddParameterGroups(parameterGroups);

            return parameterGroups.Select(x => x.Identifier).ToList();
        }

        public void AddBasicPeriodicParameterConfiguration(IClientSession clientSession,
            IReadOnlyList<Tuple<string, uint>> parameterIdentifiers)
        {
            var configSetIdentifier = Guid.NewGuid().ToString();
            var config = configSetManager.Create(connectionString, configSetIdentifier, "");
            config.AddConversion(defaultConversion);
            var parameterGroup = new ParameterGroup("Stream API");
            config.AddParameterGroup(parameterGroup);
            var applicationGroup =
                new ApplicationGroup("Stream API", "Stream API", new List<string>() { "Stream API" });
            applicationGroup.SupportsRda = false;
            config.AddGroup(applicationGroup);
            var channelsToAdd = new Dictionary<string, Tuple<uint, uint>>();
            foreach (var parameter in parameterIdentifiers)
            {
                var channelId = GenerateUniqueChannelId();
                channelsToAdd[parameter.Item1] = new Tuple<uint, uint>(parameter.Item2, channelId);
                var parameterChannel = new Channel(channelId, parameter.Item1, parameter.Item2, DataType.Double64Bit,
                    ChannelDataSourceType.Periodic, parameter.Item1);
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
                    new List<string>() { applicationGroup.Name },
                    new List<uint>() { channelId },
                    applicationGroup.Name
                );
                config.AddParameter(parameterObj);
            }

            Console.WriteLine($"Commiting config {config.Identifier}.");
            try
            {
                lock (configLock)
                {
                    config.Commit();
                }

                clientSession.Session.UseLoggingConfigurationSet(config.Identifier);
                Console.WriteLine(
                    $"Successfully added configuration {configSetIdentifier} for {parameterIdentifiers.Count} parameters.");

                foreach (var parameter in channelsToAdd)
                {
                    if (channelIdPeriodicParameterDictionary.ContainsKey(parameter.Key))
                    {
                        channelIdPeriodicParameterDictionary[parameter.Key].Add(parameter.Value.Item1, parameter.Value.Item2);
                    }
                    else
                    {
                        channelIdPeriodicParameterDictionary[parameter.Key] = new Dictionary<uint, uint>
                            { { parameter.Value.Item1, parameter.Value.Item2 } };
                    }
                }
                     
            }
            catch (ConfigurationSetAlreadyExistsException)
            {
                Console.WriteLine($"Config {configSetIdentifier} already exists.");
            }
        }

        public void AddBasicParameterConfiguration(IClientSession clientSession,
            IReadOnlyList<string> parameterIdentifiers)
        {
            var configSetIdentifier = Guid.NewGuid().ToString();
            var config = configSetManager.Create(connectionString, configSetIdentifier, "");
            config.AddConversion(defaultConversion);
            var parameterGroup = new ParameterGroup("Stream API");
            config.AddParameterGroup(parameterGroup);
            var applicationGroup =
                new ApplicationGroup("Stream API", "Stream API", new List<string>() { "Stream API" });
            applicationGroup.SupportsRda = false;
            config.AddGroup(applicationGroup);
            var channelsToAdd = new Dictionary<string, uint>();
            foreach (var parameterIdentifier in parameterIdentifiers)
            {
                var channelId = GenerateUniqueChannelId();
                channelsToAdd[parameterIdentifier] = channelId;
                var parameterChannel = new Channel(channelId, parameterIdentifier, 0, DataType.Double64Bit,
                    ChannelDataSourceType.RowData, parameterIdentifier);
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
                    new List<string>() { applicationGroup.Name },
                    new List<uint>() { channelId },
                    applicationGroup.Name
                );
                config.AddParameter(parameter);
            }

            Console.WriteLine($"Commiting config {config.Identifier}.");
            try
            {
                lock (configLock)
                {
                    config.Commit();
                }

                clientSession.Session.UseLoggingConfigurationSet(config.Identifier);
                Console.WriteLine(
                    $"Successfully added configuration {configSetIdentifier} for {parameterIdentifiers.Count} parameters.");
                foreach (var parameter in channelsToAdd) channelIdParameterDictionary[parameter.Key] = parameter.Value;
            }
            catch (ConfigurationSetAlreadyExistsException)
            {
                Console.WriteLine($"Config {configSetIdentifier} already exists.");
            }
        }

        public void AddBasicEventConfiguration(IClientSession clientSession, IReadOnlyList<string> eventIdentifiers)
        {
            var configSetIdentifier = Guid.NewGuid().ToString();
            var config = configSetManager.Create(connectionString, configSetIdentifier, "");
            config.AddConversion(defaultConversion);
            var parameterGroup = new ParameterGroup("Stream API");
            config.AddParameterGroup(parameterGroup);
            var applicationGroup =
                new ApplicationGroup("Stream API", "Stream API", new List<string>() { "Stream API" });
            applicationGroup.SupportsRda = false;
            config.AddGroup(applicationGroup);
            var eventsToAdd = new Dictionary<string, EventDefinition>();
            foreach (string eventIdentifier in eventIdentifiers)
            {
                var eventDefId = Random.Shared.Next(0, 600);
                var eventDefinitionSql = new EventDefinition(
                    eventDefId,
                    eventIdentifier,
                    EventPriorityType.Low,
                    new List<string>() { "DefaultConversion", "DefaultConversion", "DefaultConversion" },
                    "Stream API"
                );
                config.AddEventDefinition(eventDefinitionSql);
                eventsToAdd[eventIdentifier] = eventDefinitionSql;
            }

            Console.WriteLine($"Commiting config {config.Identifier}.");
            try
            {
                lock (configLock)
                {
                    config.Commit();
                }

                clientSession.Session.UseLoggingConfigurationSet(config.Identifier);

                foreach (var events in eventsToAdd) eventDefCache[events.Key] = events.Value;

                Console.WriteLine(
                    $"Successfully added configuration {configSetIdentifier} for {eventIdentifiers.Count} events");
            }
            catch (ConfigurationSetAlreadyExistsException)
            {
                Console.WriteLine($"Config {configSetIdentifier} already exists.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to add config due to {ex.Message}");
            }
        }

        private readonly object _channelLock = new();

        private uint GenerateUniqueChannelId()
        {
            lock (_channelLock)
            {
                var channelId = currentChannelId % 2147483647;
                currentChannelId++;
                return channelId;
            }
        }

        public bool IsParameterExistInConfig(string parameterName)
        {
            return channelIdParameterDictionary.ContainsKey( parameterName );
        }

        public bool IsParameterExistInConfig(string parameterName, uint interval)
        {
            return channelIdPeriodicParameterDictionary.ContainsKey(parameterName) && channelIdPeriodicParameterDictionary[parameterName].ContainsKey(interval);
        }

        public bool TryAddPeriodicData(IClientSession clientSession, string parameterIdentifier, List<double> data,
            long timestamp, uint interval)
        {
            try
            {
                var dataBytes = data.SelectMany(BitConverter.GetBytes).ToArray();
                sampleCounter += data.Count;
                if (sampleCounter % 10000 == 0)
                {
                    Console.WriteLine($"From Periodic, total sample count: {sampleCounter}");
                }
                lock (configLock)
                {
                    clientSession.Session.AddChannelData(channelIdPeriodicParameterDictionary[parameterIdentifier][interval],
                        timestamp, data.Count, dataBytes);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write periodic data {parameterIdentifier} due to {ex.Message}.");
                return false;
            }
        }

        public bool TryAddData(IClientSession clientSession, RepeatedField<string> parameterList, List<double> data,
            long timestamp)
        {
            try
            {
                var channelIds = parameterList.Select(x => channelIdParameterDictionary[x]).ToList();

                var dataBytes = data.SelectMany(BitConverter.GetBytes).ToArray();

                sampleCounter += data.Count;
                if (sampleCounter % 10000 == 0)
                {
                    Console.WriteLine($"From Row Data Total Sample count: {sampleCounter}");
                }

                lock (configLock)
                {
                    clientSession.Session.AddRowData(timestamp, channelIds, dataBytes);
                }

                return true;

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write row data due to {ex.Message}.");
                return false;
            }
        }

        public void AddLap(IClientSession clientSession, long timestamp, short lapNumber, string lapName,
            bool countForFastestLap)
        {
            var newLap = new Lap(
                timestamp,
                lapNumber,
                BitConverter.GetBytes(0)[0],
                lapName,
                countForFastestLap
            );

            try
            {
                lock (configLock)
                {
                    clientSession.Session.LapCollection.Add(newLap);
                }

                Console.WriteLine($"Added lap {lapName} at {timestamp}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to add Lap {lapName} due to {ex.Message}");
            }
        }

        public void CloseSession(IClientSession clientSession)
        {
            var identifier = clientSession.Session.Identifier;
            if (clientSession == null) return;
            clientSession.Session.EndData();
            clientSession.Close();
            Console.WriteLine($"Closed Session {identifier}.");
        }

        public void AddDetails(IClientSession clientSession, string key, string value)
        {
            var sessionDetailItem = new SessionDataItem(key, value);
            clientSession.Session.Items.Add(sessionDetailItem);
        }

        public void AddMarker(IClientSession clientSession, long timestamp, string label)
        {
            try
            {
                var marker = new Marker(timestamp, label);
                lock (configLock)
                {
                    clientSession.Session.Markers.Add(marker);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to Add Marker {label} due to {ex.Message}");
            }
        }

        public bool TryAddEvent(IClientSession clientSession, string eventIdentifier, long timestamp,
            IList<double> data,
            string groupName = "Stream API")
        {
            try
            {
                lock (configLock)
                {
                    clientSession.Session.Events.AddEventData(eventDefCache[eventIdentifier].EventDefinitionId,
                        groupName,
                        timestamp, data);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to add event {eventIdentifier} due to {ex.Message}");
                return false;
            }
        }

        public void UpdateSessionInfo(IClientSession clientSession, GetSessionInfoResponse sessionInfo)
        {
            clientSession.Session.UpdateIdentifier(sessionInfo.Identifier);
        }
    }
}