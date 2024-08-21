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
using Parameter = MESL.SqlRace.Domain.Parameter;

namespace Stream.Api.Stream.Reader
{
    internal class AtlasSessionWriter
    {
        private readonly string _connectionString;
        private readonly Dictionary<EventPriority, EventPriorityType> _eventPriorityDictionary =
            new()
            {
                { EventPriority.Critical, EventPriorityType.High },
                { EventPriority.High, EventPriorityType.High },
                { EventPriority.Medium, EventPriorityType.Medium },
                { EventPriority.Low, EventPriorityType.Low },
                { EventPriority.Debug, EventPriorityType.Debug },
                { EventPriority.Unspecified, EventPriorityType.Low }
            };
        private readonly ConcurrentDictionary<string, uint> _channelIdRowParameterDictionary = new();
        private readonly ConcurrentDictionary<string, Dictionary<uint, uint>> _channelIdPeriodicParameterDictionary = new();
        private readonly object _configLock = new();
        private ConfigurationSetManager? _configSetManager;
        private uint _currentChannelId = 0;
        private Guid _recorderGuid;
        private readonly RationalConversion _defaultConversion =
            RationalConversion.CreateSimple1To1Conversion("DefaultConversion", "", "%5.2f");
        private SessionManager? _sessionManager;
        private int _sampleCounter = 0;

        public ConcurrentDictionary<string, EventDefinition> EventDefCache { get; }

        public AtlasSessionWriter(string connectionString)
        {
            this._connectionString = connectionString;
            this.EventDefCache = new ConcurrentDictionary<string, EventDefinition>();
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
            Console.WriteLine($"New Session is created with name {sessionName}.");
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
            this._recorderGuid = Guid.NewGuid();
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
            recorderConfiguration.RemoveConfiguration(this._recorderGuid);
            _sessionManager?.ServerListener.Stop();
        }

        public void AddBasicPeriodicParameterConfiguration(IClientSession clientSession,
            IReadOnlyList<Tuple<string, uint>> parameterIdentifiers)
        {
            if (_configSetManager == null)
            {
                return;
            }
            var configSetIdentifier = Guid.NewGuid().ToString();
            var config = _configSetManager.Create(_connectionString, configSetIdentifier, "");
            config.AddConversion(_defaultConversion);
            var parameterGroup = new ParameterGroup("Stream API");
            config.AddParameterGroup(parameterGroup);
            var applicationGroup =
                new ApplicationGroup("Stream API", "Stream API", new List<string>() { "Stream API" })
                {
                    SupportsRda = false
                };
            config.AddGroup(applicationGroup);
            var channelsToAdd = new Dictionary<string, Tuple<uint, uint>>();
            foreach (var parameter in parameterIdentifiers)
            {
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
                Console.WriteLine(
                    $"Successfully added configuration {configSetIdentifier} for {parameterIdentifiers.Count} parameters.");

                foreach (var parameter in channelsToAdd)
                {
                    if (_channelIdPeriodicParameterDictionary.TryGetValue(parameter.Key, out var intervals))
                    {
                        intervals.Add(parameter.Value.Item1, parameter.Value.Item2);
                        _channelIdPeriodicParameterDictionary[parameter.Key] = intervals;
                    }
                    else
                    {
                        _channelIdPeriodicParameterDictionary[parameter.Key] = new Dictionary<uint, uint>
                            { { parameter.Value.Item1, parameter.Value.Item2 } };
                    }
                }
                     
            }
            catch (ConfigurationSetAlreadyExistsException)
            {
                Console.WriteLine($"Config {configSetIdentifier} already exists.");
            }
        }

        public void AddBasicRowParameterConfiguration(IClientSession clientSession,
            IReadOnlyList<string> parameterIdentifiers)
        {
            if (_configSetManager == null)
            {
                return;
            }
            var configSetIdentifier = Guid.NewGuid().ToString();
            var config = _configSetManager.Create(_connectionString, configSetIdentifier, "");

            config.AddConversion(_defaultConversion);

            var parameterGroup = new ParameterGroup("Stream API");
            config.AddParameterGroup(parameterGroup);

            var applicationGroup =
                new ApplicationGroup("Stream API", "Stream API", new List<string>() { "Stream API" })
                {
                    SupportsRda = false
                };
            config.AddGroup(applicationGroup);

            var channelsToAdd = new Dictionary<string, uint>();
            foreach (var parameterIdentifier in parameterIdentifiers)
            {
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
                Console.WriteLine(
                    $"Successfully added configuration {configSetIdentifier} for {parameterIdentifiers.Count} parameters.");
                foreach (var parameter in channelsToAdd) _channelIdRowParameterDictionary[parameter.Key] = parameter.Value;
            }
            catch (ConfigurationSetAlreadyExistsException)
            {
                Console.WriteLine($"Config {configSetIdentifier} already exists.");
            }
        }

        public void AddBasicEventConfiguration(IClientSession clientSession, IReadOnlyList<string> eventIdentifiers)
        {
            if (_configSetManager == null)
            {
                return;
            }
            var configSetIdentifier = Guid.NewGuid().ToString();
            var config = _configSetManager.Create(_connectionString, configSetIdentifier, "");
            config.AddConversion(_defaultConversion);

            var parameterGroup = new ParameterGroup("Stream API");
            config.AddParameterGroup(parameterGroup);

            var applicationGroup =
                new ApplicationGroup("Stream API", "Stream API", new List<string>() { "Stream API" })
                {
                    SupportsRda = false
                };
            config.AddGroup(applicationGroup);

            var eventsToAdd = new Dictionary<string, EventDefinition>();
            foreach (var eventIdentifier in eventIdentifiers)
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
                lock (_configLock)
                {
                    config.Commit();
                }

                clientSession.Session.UseLoggingConfigurationSet(config.Identifier);

                foreach (var events in eventsToAdd) EventDefCache[events.Key] = events.Value;

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

        private uint GenerateUniqueChannelId(IClientSession clientSession)
        {
            lock (_channelLock)
            {
                return clientSession.Session.ReserveNextAvailableRowChannelId() % 2147483647;
            }
        }

        public bool IsParameterExistInConfig(string parameterName)
        {
            return _channelIdRowParameterDictionary.ContainsKey(parameterName);
        }

        public bool IsParameterExistInConfig(string parameterName, uint interval)
        {
            return _channelIdPeriodicParameterDictionary.ContainsKey(parameterName) && _channelIdPeriodicParameterDictionary[parameterName].ContainsKey(interval);
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
                    clientSession.Session.AddChannelData(_channelIdPeriodicParameterDictionary[parameterIdentifier][interval],
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
                var channelIds = parameterList.Select(x => _channelIdRowParameterDictionary[x]).ToList();

                var dataBytes = data.SelectMany(BitConverter.GetBytes).ToArray();

                _sampleCounter += data.Count;
                if (_sampleCounter % 10000 == 0)
                {
                    Console.WriteLine($"From Row Data Total Sample count: {_sampleCounter}");
                }

                lock (_configLock)
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
                lock (_configLock)
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

        public static void CloseSession(IClientSession clientSession)
        {
            var identifier = clientSession.Session.Identifier;
            clientSession.Session.EndData();
            clientSession.Close();
            Console.WriteLine($"Closed Session {identifier}.");
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
                var marker = new Marker(timestamp, label);
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
            IList<double> data,
            string groupName = "Stream API")
        {
            try
            {
                lock (_configLock)
                {
                    clientSession.Session.Events.AddEventData(EventDefCache[eventIdentifier].EventDefinitionId,
                        groupName,
                        timestamp,
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