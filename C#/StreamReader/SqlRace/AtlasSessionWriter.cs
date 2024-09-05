// <copyright file="AtlasSessionWriter.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using Google.Protobuf.Collections;
using MA.Streaming.API;
using MA.Streaming.OpenData;
using MAT.OCS.Core;
using MESL.SqlRace.Domain;
using MESL.SqlRace.Enumerators;
using DataType = MESL.SqlRace.Enumerators.DataType;
using EventDefinition = MESL.SqlRace.Domain.EventDefinition;
using Parameter = MESL.SqlRace.Domain.Parameter;

namespace Stream.Api.Stream.Reader.SqlRace
{
    internal class AtlasSessionWriter
    {
        private readonly string _connectionString;
        private const long NumberOfNanosecondsInDay = 86400000000000;
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

        private readonly object _configLock = new();
        private ConfigurationSetManager? _configSetManager;
        private Guid _recorderGuid;
        private readonly RationalConversion _defaultConversion =
            RationalConversion.CreateSimple1To1Conversion("DefaultConversion", "", "%5.2f");
        private SessionManager? _sessionManager;
        private int _sampleCounter = 0;

        private ConcurrentDictionary<SessionKey, SessionConfig> channelIdCache = new();

        public AtlasSessionWriter(string connectionString)
        {
            _connectionString = connectionString;
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

            _sessionManager = SessionManager.CreateSessionManager();
            _configSetManager = new ConfigurationSetManager();
        }

        public IClientSession? CreateSession(string sessionName, string sessionType)
        {
            if (!Core.IsInitialized)
            {
                Console.WriteLine("Can't create session if SQL Race is not initialized.");
                return null;
            }
            StartRecorder();
            var sessionKey = SessionKey.NewKey();
            var sessionDate = DateTime.Now;
            var clientSession = _sessionManager?.CreateSession(_connectionString, sessionKey, sessionName,
                sessionDate, sessionType);
            this.channelIdCache[clientSession.Session.Key] = new SessionConfig();
            Console.WriteLine($"New SqlRaceSession is created with name {sessionName}.");
            return clientSession;
        }

        private void StartRecorder(int port = 7300)
        {
            if (_sessionManager == null)
            {
                return;
            }
            if (!_sessionManager.ServerListener.IsRunning)
            {
                Core.ConfigureServer(true, IPEndPoint.Parse($"127.0.0.1:{port}"));
                Console.WriteLine($"Sever Listener is Running on {_sessionManager.ServerListener.ServerEndPoint}");
            }
            else
            {
                Console.WriteLine(
                    $"Server listener is already running on {_sessionManager.ServerListener.ServerEndPoint}");
            }

            var recorderConfiguration = RecordersConfiguration.GetRecordersConfiguration();
            _recorderGuid = Guid.NewGuid();
            recorderConfiguration.AddConfiguration(
                _recorderGuid,
                "SQLServer",
                _connectionString,
                _connectionString,
                _connectionString,
                false
            );
        }

        public void StopRecorderAndServer()
        {
            var recorderConfiguration = RecordersConfiguration.GetRecordersConfiguration();
            recorderConfiguration.RemoveConfiguration(_recorderGuid);
            _sessionManager?.ServerListener.Stop();
        }

        public void AddBasicPeriodicParameterConfiguration(IClientSession clientSession,
            IReadOnlyList<Tuple<string, uint>> parameterIdentifiers)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            Console.WriteLine($"Adding config for {parameterIdentifiers.Count} periodic parameters.");
            if (_configSetManager == null)
            {
                return;
            }
            var configSetIdentifier = Guid.NewGuid().ToString();
            var config = _configSetManager.Create(_connectionString, configSetIdentifier, "");
            config.AddConversion(_defaultConversion);

            var channelsToAdd = new Dictionary<string, Tuple<uint, uint>>();
            foreach (var parameter in parameterIdentifiers)
            {
                var parameterGroup = new ParameterGroup(parameter.Item1.Split(':')[1]);
                config.AddParameterGroup(parameterGroup);
                var applicationGroup =
                    new ApplicationGroup(parameter.Item1.Split(':')[1], parameter.Item1.Split(':')[1], new List<string>() { parameterGroup.Identifier })
                    {
                        SupportsRda = false
                    };
                config.AddGroup(applicationGroup);

                var channelId = GenerateUniqueChannelId(clientSession);
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
                lock (_configLock)
                {
                    config.Commit();
                }

                clientSession.Session.UseLoggingConfigurationSet(config.Identifier);
                
                foreach (var parameter in channelsToAdd)
                {
                    if (channelIdCache[clientSession.Session.Key].ChannelIdPeriodicParameterDictionary.TryGetValue(parameter.Key, out var intervals))
                    {
                        intervals.Add(parameter.Value.Item1, parameter.Value.Item2);
                        channelIdCache[clientSession.Session.Key].ChannelIdPeriodicParameterDictionary[parameter.Key] = intervals;
                    }
                    else
                    {
                        channelIdCache[clientSession.Session.Key].ChannelIdPeriodicParameterDictionary[parameter.Key] = new Dictionary<uint, uint>
                            { { parameter.Value.Item1, parameter.Value.Item2 } };
                    }
                }
                stopwatch.Stop();
                Console.WriteLine(
                    $"Successfully added configuration {configSetIdentifier} for {parameterIdentifiers.Count} periodic parameters. Time Taken: {stopwatch.ElapsedMilliseconds} ms.");

            }
            catch (ConfigurationSetAlreadyExistsException)
            {
                Console.WriteLine($"Config {configSetIdentifier} already exists.");
            }
        }

        public void AddBasicRowParameterConfiguration(IClientSession clientSession,
            IReadOnlyList<string> parameterIdentifiers)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            Console.WriteLine($"Adding config for {parameterIdentifiers.Count} row parameters.");
            if (_configSetManager == null)
            {
                return;
            }
            var configSetIdentifier = Guid.NewGuid().ToString();
            var config = _configSetManager.Create(_connectionString, configSetIdentifier, "");

            config.AddConversion(_defaultConversion);

            var channelsToAdd = new Dictionary<string, uint>();
            foreach (var parameterIdentifier in parameterIdentifiers)
            {
                var parameterGroup = new ParameterGroup(parameterIdentifier.Split(':')[1]);
                config.AddParameterGroup(parameterGroup);

                var applicationGroup =
                    new ApplicationGroup(parameterIdentifier.Split(':')[1], parameterIdentifier.Split(':')[1], new List<string>() { parameterGroup.Identifier })
                    {
                        SupportsRda = false
                    };
                config.AddGroup(applicationGroup);

                var channelId = GenerateUniqueChannelId(clientSession);
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
                lock (_configLock)
                {
                    config.Commit();
                }

                clientSession.Session.UseLoggingConfigurationSet(config.Identifier);

                foreach (var parameter in channelsToAdd)
                    channelIdCache[clientSession.Session.Key].ChannelIdRowParameterDictionary[parameter.Key] =
                        parameter.Value;
                stopwatch.Stop();
                Console.WriteLine(
                    $"Successfully added configuration {configSetIdentifier} for {parameterIdentifiers.Count} row parameters. Time taken: {stopwatch.ElapsedMilliseconds} ms.");
            }
            catch (ConfigurationSetAlreadyExistsException)
            {
                Console.WriteLine($"Config {configSetIdentifier} already exists.");
            }
        }

        public void AddBasicEventConfiguration(IClientSession clientSession, IReadOnlyList<string> eventIdentifiers)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            Console.WriteLine($"Adding config for {eventIdentifiers.Count} events.");   
            if (_configSetManager == null)
            {
                return;
            }
            var configSetIdentifier = Guid.NewGuid().ToString();
            var config = _configSetManager.Create(_connectionString, configSetIdentifier, "");
            config.AddConversion(_defaultConversion);

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
                    new List<string>() { "DefaultConversion", "DefaultConversion", "DefaultConversion" },
                    appGroupName
                );
                config.AddEventDefinition(eventDefinitionSql);
                eventsToAdd[eventIdentifier] = eventDefinitionSql;
            }

            Console.WriteLine($"Commiting config {config.Identifier}.");
            try
            {
                lock (_configLock)
                {
                    config.Commit();
                }

                clientSession.Session.UseLoggingConfigurationSet(config.Identifier);

                foreach (var events in eventsToAdd) channelIdCache[clientSession.Session.Key].EventDefCache[events.Key] = events.Value;
                stopwatch.Stop();
                Console.WriteLine(
                    $"Successfully added configuration {configSetIdentifier} for {eventIdentifiers.Count} events. Time Taken: {stopwatch.ElapsedMilliseconds}.");
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

        private uint GenerateUniqueChannelId(IClientSession clientSession)
        {
            lock (_channelLock)
            {
                return clientSession.Session.ReserveNextAvailableRowChannelId() % 2147483647;
            }
        }

        public bool IsParameterExistInConfig(string parameterName, IClientSession clientSession)
        {
            return channelIdCache[clientSession.Session.Key].ChannelIdRowParameterDictionary.ContainsKey(parameterName);
        }

        public bool IsParameterExistInConfig(string parameterName, uint interval, IClientSession clientSession)
        {
            return channelIdCache[clientSession.Session.Key].ChannelIdPeriodicParameterDictionary
                .ContainsKey(parameterName) && channelIdCache[clientSession.Session.Key]
                .ChannelIdPeriodicParameterDictionary[parameterName].ContainsKey(interval);
        }

        public bool IsEventExistInConfig(string eventName, IClientSession clientSession)
        {
            return channelIdCache[clientSession.Session.Key].EventDefCache.ContainsKey(eventName);
        }

        public bool TryAddPeriodicData(IClientSession clientSession, string parameterIdentifier, List<double> data,
            long timestamp, uint interval)
        {
            try
            {
                var dataBytes = data.SelectMany(BitConverter.GetBytes).ToArray();
                _sampleCounter += data.Count;
                if (_sampleCounter % 10000 == 0)
                {
                    Console.WriteLine($"From Periodic, total sample count: {_sampleCounter}");
                }
                lock (_configLock)
                {
                    clientSession.Session.AddChannelData(channelIdCache[clientSession.Session.Key].ChannelIdPeriodicParameterDictionary[parameterIdentifier][interval],
                        timestamp % NumberOfNanosecondsInDay, data.Count, dataBytes);
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
                var channelIds = parameterList.Select(x => channelIdCache[clientSession.Session.Key].ChannelIdRowParameterDictionary[x]).ToList();

                var dataBytes = data.SelectMany(BitConverter.GetBytes).ToArray();

                _sampleCounter += data.Count;
                if (_sampleCounter % 10000 == 0)
                {
                    Console.WriteLine($"From Row Data Total Sample count: {_sampleCounter}");
                }

                lock (_configLock)
                {
                    clientSession.Session.AddRowData(timestamp % NumberOfNanosecondsInDay, channelIds, dataBytes);
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
                timestamp % NumberOfNanosecondsInDay,
                lapNumber,
                BitConverter.GetBytes(0)[0],
                lapName,
                countForFastestLap
            );

            try
            {
                lock (_configLock)
                {
                    clientSession.Session.LapCollection.Add(newLap);
                }

                Console.WriteLine($"Added lap {lapName} at {timestamp % NumberOfNanosecondsInDay}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to add Lap {lapName} due to {ex.Message}");
            }
        }

        public void CloseSession(IClientSession clientSession)
        {
            if (clientSession.Session == null)
            {
                return;
            }
            var identifier = clientSession.Session.Identifier;
            clientSession.Session.EndData(); ;
            channelIdCache.TryRemove(clientSession.Session.Key, out var sessionConfig);
            sessionConfig?.Dispose();
            clientSession.Close();
            Console.WriteLine($"Closed SqlRaceSession {identifier}.");
        }

        public static void AddDetails(IClientSession clientSession, string key, string value)
        {
            var sessionDetailItem = new SessionDataItem(key, value);
            clientSession.Session.Items.Add(sessionDetailItem);
        }

        public void AddMarker(IClientSession clientSession, long timestamp, string label)
        {
            try
            {
                var marker = new Marker(timestamp % NumberOfNanosecondsInDay, label);
                lock (_configLock)
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
            IList<double> data)
        {
            try
            {
                lock (_configLock)
                {
                    clientSession.Session.Events.AddEventData(channelIdCache[clientSession.Session.Key].EventDefCache[eventIdentifier].EventDefinitionId,
                        channelIdCache[clientSession.Session.Key].EventDefCache[eventIdentifier].GroupName,
                        timestamp % NumberOfNanosecondsInDay,
                        data);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to add event {eventIdentifier} due to {ex.Message}");
                return false;
            }
        }

        public static void UpdateSessionInfo(IClientSession clientSession, GetSessionInfoResponse sessionInfo)
        {
            clientSession.Session.UpdateIdentifier(sessionInfo.Identifier);
        }
    }
}