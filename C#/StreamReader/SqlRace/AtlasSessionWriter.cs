// <copyright file="AtlasSessionWriter.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;

using Google.Protobuf.Collections;

using MA.DataPlatform.Secu4.Routing.Contracts;
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
        private const long NumberOfNanosecondsInDay = 86400000000000;
        private readonly string connectionString;
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

        private readonly object configLock = new();
        private readonly RationalConversion defaultConversion =
            RationalConversion.CreateSimple1To1Conversion("DefaultConversion", "", "%5.2f");

        private readonly object channelLock = new();
        private ConfigurationSetManager? configSetManager;
        private Guid recorderGuid;
        private SessionManager? sessionManager;
        private int sampleCounter = 0;

        private readonly ConcurrentDictionary<SessionKey, SessionConfig> channelIdCache = new();

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

            this.sessionManager = SessionManager.CreateSessionManager();
            this.configSetManager = new ConfigurationSetManager();
        }

        public IClientSession? CreateSession(string sessionName, string sessionType)
        {
            if (!Core.IsInitialized)
            {
                Console.WriteLine("Can't create session if SQL Race is not initialized.");
                return null;
            }

            this.StartRecorder();
            var sessionKey = SessionKey.NewKey();
            var sessionDate = DateTime.Now;
            var clientSession = this.sessionManager?.CreateSession(
                this.connectionString,
                sessionKey,
                sessionName,
                sessionDate,
                sessionType);
            this.channelIdCache[clientSession.Session.Key] = new SessionConfig();
            Console.WriteLine($"New SqlRaceSession is created with name {sessionName}.");
            return clientSession;
        }

        public void StopRecorderAndServer()
        {
            var recorderConfiguration = RecordersConfiguration.GetRecordersConfiguration();
            recorderConfiguration.RemoveConfiguration(this.recorderGuid);
            this.sessionManager?.ServerListener.Stop();
        }

        public void AddBasicPeriodicParameterConfiguration(
            IClientSession clientSession,
            IReadOnlyList<Tuple<string, uint>> parameterIdentifiers)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            Console.WriteLine($"Adding config for {parameterIdentifiers.Count} periodic parameters.");
            if (this.configSetManager == null)
            {
                return;
            }

            var configSetIdentifier = Guid.NewGuid().ToString();
            var config = this.configSetManager.Create(this.connectionString, configSetIdentifier, "");
            config.AddConversion(this.defaultConversion);

            var channelsToAdd = new Dictionary<string, Tuple<uint, uint>>();
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

                var channelId = this.GenerateUniqueChannelId(clientSession);
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

            Console.WriteLine($"Commiting config {config.Identifier}.");
            try
            {
                lock (this.configLock)
                {
                    config.Commit();
                }

                clientSession.Session.UseLoggingConfigurationSet(config.Identifier);

                foreach (var parameter in channelsToAdd)
                {
                    if (this.channelIdCache[clientSession.Session.Key].ChannelIdPeriodicParameterDictionary.TryGetValue(parameter.Key, out var intervals))
                    {
                        intervals.Add(parameter.Value.Item1, parameter.Value.Item2);
                        this.channelIdCache[clientSession.Session.Key].ChannelIdPeriodicParameterDictionary[parameter.Key] = intervals;
                    }
                    else
                    {
                        this.channelIdCache[clientSession.Session.Key].ChannelIdPeriodicParameterDictionary[parameter.Key] = new Dictionary<uint, uint>
                        {
                            {
                                parameter.Value.Item1, parameter.Value.Item2
                            }
                        };
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

        public void AddBasicRowParameterConfiguration(
            IClientSession clientSession,
            IReadOnlyList<string> parameterIdentifiers)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            Console.WriteLine($"Adding config for {parameterIdentifiers.Count} row parameters.");
            if (this.configSetManager == null)
            {
                return;
            }

            var configSetIdentifier = Guid.NewGuid().ToString();
            var config = this.configSetManager.Create(this.connectionString, configSetIdentifier, "");

            config.AddConversion(this.defaultConversion);

            var channelsToAdd = new Dictionary<string, uint>();
            foreach (var parameterIdentifier in parameterIdentifiers)
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

                var channelId = this.GenerateUniqueChannelId(clientSession);
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
                lock (this.configLock)
                {
                    config.Commit();
                }

                clientSession.Session.UseLoggingConfigurationSet(config.Identifier);

                foreach (var parameter in channelsToAdd)
                {
                    this.channelIdCache[clientSession.Session.Key].ChannelIdRowParameterDictionary[parameter.Key] =
                        parameter.Value;
                }

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
            if (this.configSetManager == null)
            {
                return;
            }

            var configSetIdentifier = Guid.NewGuid().ToString();
            var config = this.configSetManager.Create(this.connectionString, configSetIdentifier, "");
            config.AddConversion(this.defaultConversion);

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
                lock (this.configLock)
                {
                    config.Commit();
                }

                clientSession.Session.UseLoggingConfigurationSet(config.Identifier);

                foreach (var events in eventsToAdd)
                {
                    this.channelIdCache[clientSession.Session.Key].EventDefCache[events.Key] = events.Value;
                }

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

        public bool IsParameterExistInConfig(string parameterName, IClientSession clientSession)
        {
            return this.channelIdCache[clientSession.Session.Key].ChannelIdRowParameterDictionary.ContainsKey(parameterName);
        }

        public bool IsParameterExistInConfig(string parameterName, uint interval, IClientSession clientSession)
        {
            return this.channelIdCache[clientSession.Session.Key].ChannelIdPeriodicParameterDictionary
                .ContainsKey(parameterName) && this.channelIdCache[clientSession.Session.Key]
                .ChannelIdPeriodicParameterDictionary[parameterName].ContainsKey(interval);
        }

        public bool IsEventExistInConfig(string eventName, IClientSession clientSession)
        {
            return this.channelIdCache[clientSession.Session.Key].EventDefCache.ContainsKey(eventName);
        }

        public bool TryAddPeriodicData(
            IClientSession clientSession,
            string parameterIdentifier,
            List<double> data,
            long timestamp,
            uint interval)
        {
            try
            {
                var dataBytes = data.SelectMany(BitConverter.GetBytes).ToArray();
                this.sampleCounter += data.Count;
                if (this.sampleCounter % 10000 == 0)
                {
                    Console.WriteLine($"From Periodic, total sample count: {this.sampleCounter}");
                }

                lock (this.configLock)
                {
                    clientSession.Session.AddChannelData(
                        this.channelIdCache[clientSession.Session.Key].ChannelIdPeriodicParameterDictionary[parameterIdentifier][interval],
                        timestamp % NumberOfNanosecondsInDay,
                        data.Count,
                        dataBytes);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write periodic data {parameterIdentifier} due to {ex.Message}.");
                return false;
            }
        }

        public bool TryAddData(
            IClientSession clientSession,
            RepeatedField<string> parameterList,
            List<double> data,
            long timestamp)
        {
            try
            {
                var channelIds = parameterList.Select(x => this.channelIdCache[clientSession.Session.Key].ChannelIdRowParameterDictionary[x]).ToList();

                var dataBytes = data.SelectMany(BitConverter.GetBytes).ToArray();

                this.sampleCounter += data.Count;
                if (this.sampleCounter % 10000 == 0)
                {
                    Console.WriteLine($"From Row Data Total Sample count: {this.sampleCounter}");
                }

                lock (this.configLock)
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

        public void AddLap(
            IClientSession clientSession,
            long timestamp,
            short lapNumber,
            string lapName,
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
                lock (this.configLock)
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
            clientSession.Session.EndData();
            ;
            this.channelIdCache.TryRemove(clientSession.Session.Key, out var sessionConfig);
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
                lock (this.configLock)
                {
                    clientSession.Session.Markers.Add(marker);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to Add Marker {label} due to {ex.Message}");
            }
        }

        public bool TryAddEvent(
            IClientSession clientSession,
            string eventIdentifier,
            long timestamp,
            IList<double> data)
        {
            try
            {
                lock (this.configLock)
                {
                    clientSession.Session.Events.AddEventData(
                        this.channelIdCache[clientSession.Session.Key].EventDefCache[eventIdentifier].EventDefinitionId,
                        this.channelIdCache[clientSession.Session.Key].EventDefCache[eventIdentifier].GroupName,
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

        private void StartRecorder(int port = 7300)
        {
            if (this.sessionManager == null)
            {
                return;
            }

            if (!this.sessionManager.ServerListener.IsRunning)
            {
                Core.ConfigureServer(true, IPEndPoint.Parse($"127.0.0.1:{port}"));
                Console.WriteLine($"Sever Listener is Running on {this.sessionManager.ServerListener.ServerEndPoint}");
            }
            else
            {
                Console.WriteLine(
                    $"Server listener is already running on {this.sessionManager.ServerListener.ServerEndPoint}");
            }

            var recorderConfiguration = RecordersConfiguration.GetRecordersConfiguration();
            this.recorderGuid = Guid.NewGuid();
            recorderConfiguration.AddConfiguration(
                this.recorderGuid,
                "SQLServer",
                this.connectionString,
                this.connectionString,
                this.connectionString,
                false
            );
        }

        private uint GenerateUniqueChannelId(IClientSession clientSession)
        {
            lock (this.channelLock)
            {
                return clientSession.Session.ReserveNextAvailableRowChannelId() % 2147483647;
            }
        }
    }
}

internal interface ISqlRaceData
{}

public class SqlRaceEvent: ISqlRaceData
{
    public SqlRaceEvent(int eventDefinitionKey, string groupName, long timestamp, IReadOnlyList<double> data)
    {
        this.EventDefinitionKey = eventDefinitionKey;
        this.GroupName = groupName;
        this.Timestamp = timestamp;
        this.Data = data;
    }

    public int EventDefinitionKey { get; }

    public string GroupName { get; }

    public long Timestamp { get; }

    public IReadOnlyList<double> Data { get; }
}


interface ISQlRaceWriter
{
    void Write(ISqlRaceData data);
}