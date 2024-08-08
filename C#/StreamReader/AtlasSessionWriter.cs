using System.Net;
using Confluent.Kafka;
using MA.Streaming.API;
using MA.Streaming.OpenData;
using MAT.OCS.Core;
using MESL.SqlRace.Domain;
using MESL.SqlRace.Domain.Events;
using MESL.SqlRace.Enumerators;
using DataType = MA.Streaming.OpenData.DataType;
using EventDefinition = MESL.SqlRace.Domain.EventDefinition;

namespace Stream.Api.Stream.Reader
{
    internal class AtlasSessionWriter
    {
        private readonly string dbLocation;
        public readonly Dictionary<string, uint> channelIdParameterDictionary = new Dictionary<string, uint>();
        private readonly string connectionString;
        public Dictionary<string, EventDefinition> eventDefCache = new Dictionary<string, EventDefinition>();
        private RationalConversion defaultConversion =
            RationalConversion.CreateSimple1To1Conversion("DefaultConversion", "", "%5.2f");

        private readonly Dictionary<EventPriority, EventPriorityType> eventPriorityDictionary =
            new Dictionary<EventPriority, EventPriorityType>()
            {
                { EventPriority.Critical, EventPriorityType.High },
                { EventPriority.High, EventPriorityType.High },
                { EventPriority.Medium, EventPriorityType.Medium },
                { EventPriority.Low, EventPriorityType.Low },
                { EventPriority.Debug, EventPriorityType.Debug },
                { EventPriority.Unspecified, EventPriorityType.Low }
            };

        private SessionManager? sessionManager;
        private ISessionEventService eventService;

        public AtlasSessionWriter(string dbLocation)
        {
            this.dbLocation = dbLocation;
            connectionString = $"DbEngine=SQLite;Data Source={this.dbLocation};Pooling=false;";
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
            eventService = EventsFactory.CreateSessionEventService();

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
                "SQLite",
                dbLocation,
                dbLocation,
                connectionString,
                false
            );
        }

        public void AddConfiguration(IClientSession clientSession, ConfigurationPacket packet)
        {
            Console.WriteLine($"Adding configuration {packet.ConfigId}.");
            var configIdentifier = packet.ConfigId;
            var configSetManager = new ConfigurationSetManager();

            if (configSetManager.Exists(new DatabaseConnectionInformation(connectionString), configIdentifier))
            {
                Console.WriteLine($"Configuration {packet.ConfigId} already exists, using existing config.");
                clientSession.Session.UseLoggingConfigurationSet(configIdentifier);
                return;
            }

            var config = configSetManager.Create(connectionString, configIdentifier, "");
            var defaultConversionFuncName = "DefaultConversion";
            config.AddConversion(
                RationalConversion.CreateSimple1To1Conversion(defaultConversionFuncName, "", "%5.2f")
            );
            var appGroups = new List<string>();
            foreach (var parameterDefinition in packet.ParameterDefinitions)
                appGroups.Add(parameterDefinition.ApplicationName);

            foreach (var app in appGroups)
            {
                var group = new ParameterGroup(app, app);
                config.AddParameterGroup(group);
                var applicationGroup = new ApplicationGroup(app, new List<string>() { group.Identifier });
                applicationGroup.SupportsRda = false;
                config.AddGroup(applicationGroup);
            }

            foreach (var parameterDefinition in packet.ParameterDefinitions)
            {
                var conversionFuncName = defaultConversionFuncName;
                var channelId = clientSession.Session.ReserveNextAvailableRowChannelId() % 2147483647;
                channelIdParameterDictionary[parameterDefinition.Identifier] = channelId;
                var parameterChannel = new Channel(channelId, "ParameterChannel", 0, MESL.SqlRace.Enumerators.DataType.Double64Bit,
                    ChannelDataSourceType.RowData);
                config.AddChannel(parameterChannel);
                if (parameterDefinition.Conversion != null)
                {
                    var inputValues = new double[parameterDefinition.Conversion.InputValues.Count];
                    var stringValues = new string[parameterDefinition.Conversion.StringValues.Count];
                    parameterDefinition.Conversion.InputValues.CopyTo(inputValues, 0);
                    parameterDefinition.Conversion.StringValues.CopyTo(stringValues, 0);
                    config.AddConversion(new TextConversion(
                        parameterDefinition.Conversion.ConversionIdentifier,
                        parameterDefinition.Units,
                        parameterDefinition.FormatString,
                        inputValues,
                        stringValues,
                        parameterDefinition.Conversion.Default
                    ));
                    conversionFuncName = parameterDefinition.Conversion.ConversionIdentifier;
                }

                var parameter = new Parameter(
                    parameterDefinition.Identifier,
                    parameterDefinition.Name,
                    parameterDefinition.Description,
                    parameterDefinition.MaxValue,
                    parameterDefinition.MinValue,
                    parameterDefinition.WarningMaxValue,
                    parameterDefinition.WarningMinValue,
                    0.0,
                    0xFFFF,
                    0,
                    conversionFuncName,
                    new List<string> { parameterDefinition.ApplicationName },
                    new List<uint> { channelId },
                    parameterDefinition.ApplicationName,
                    parameterDefinition.FormatString,
                    parameterDefinition.Units
                );
                config.AddParameter(parameter);
            }

            foreach (var eventDefinition in packet.EventDefinitions)
            {
                var conversionFuncNames = new string[]
                    { defaultConversionFuncName, defaultConversionFuncName, defaultConversionFuncName };
                for (var i = 0; i < eventDefinition.Conversions.Count; i++)
                    if (eventDefinition.Conversions[i].ConversionIdentifier != "")
                    {
                        var inputValues = new double[eventDefinition.Conversions[i].InputValues.Count];
                        var stringValues = new string[eventDefinition.Conversions[i].StringValues.Count];
                        eventDefinition.Conversions[i].InputValues.CopyTo(inputValues, 0);
                        eventDefinition.Conversions[i].StringValues.CopyTo(stringValues, 0);
                        config.AddConversion(new TextConversion(
                            eventDefinition.Conversions[i].ConversionIdentifier,
                            "",
                            "5.2f",
                            inputValues,
                            stringValues,
                            eventDefinition.Conversions[i].Default
                        ));
                        conversionFuncNames[i] = eventDefinition.Conversions[i].ConversionIdentifier;
                    }

                var eventDefId = (int)eventDefinition.DefinitionId;
                var eventDefinitionSql = new EventDefinition(
                    eventDefId,
                    eventDefinition.Description,
                    eventPriorityDictionary[eventDefinition.Priority],
                    conversionFuncNames,
                    eventDefinition.ApplicationName
                );
                config.AddEventDefinition(eventDefinitionSql);
            }

            try
            {
                config.Commit();
                Console.WriteLine($"Succesfully added configuration {configIdentifier}");
            }
            catch (ConfigurationSetAlreadyExistsException)
            {
                Console.WriteLine($"Config {configIdentifier} already exists.");
            }

            clientSession.Session.UseLoggingConfigurationSet(config.Identifier);
        }

        public void AddBasicConfiguration(IClientSession clientSession, string parameterIdentifier)
        {
            var configSetIdentifier = Guid.NewGuid().ToString();
            var configSetManager = new ConfigurationSetManager();

            var config = configSetManager.Create(connectionString, configSetIdentifier, "");
            config.AddConversion(defaultConversion);
            var parameterGroup = new ParameterGroup("Stream API");
            config.AddParameterGroup(parameterGroup);
            var applicationGroup = new ApplicationGroup("Stream API", "Stream API", new List<string>() { "Stream API" });
            applicationGroup.SupportsRda = false;
            config.AddGroup(applicationGroup);
            var channelId = clientSession.Session.ReserveNextAvailableRowChannelId() % 2147483647;
            channelIdParameterDictionary[parameterIdentifier] = channelId;
            var parameterChannel = new Channel(channelId, "Parameter Channel", 0, MESL.SqlRace.Enumerators.DataType.Double64Bit,
                ChannelDataSourceType.RowData);
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

            try
            {
                config.Commit();
                //clientSession.Session.LoadConfiguration();
                Console.WriteLine($"Successfully added configuration {configSetIdentifier}");
            }
            catch (ConfigurationSetAlreadyExistsException)
            {
                Console.WriteLine($"Config {configSetIdentifier} already exists.");
            }
            finally
            {
                clientSession.Session.UseLoggingConfigurationSet(config.Identifier);
            }
        }

        public void AddBasicParameterConfiguration(IClientSession clientSession, List<string> parameterIdentifiers)
        {
            var configSetIdentifier = Guid.NewGuid().ToString();
            var configSetManager = new ConfigurationSetManager();
            var config = configSetManager.Create(connectionString, configSetIdentifier, "");
            foreach (var parameterIdentifier in parameterIdentifiers)
            {
                config.AddConversion(defaultConversion);
                var parameterGroup = new ParameterGroup("Stream API");
                config.AddParameterGroup(parameterGroup);
                var applicationGroup = new ApplicationGroup("Stream API", "Stream API", new List<string>() { "Stream API" });
                applicationGroup.SupportsRda = false;
                config.AddGroup(applicationGroup);
                var channelId = clientSession.Session.ReserveNextAvailableRowChannelId() % 2147483647;
                channelIdParameterDictionary[parameterIdentifier] = channelId;
                var parameterChannel = new Channel(channelId, "Parameter Channel", 0, MESL.SqlRace.Enumerators.DataType.Double64Bit,
                    ChannelDataSourceType.RowData);
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
            try
            {
                config.Commit();
                Console.WriteLine($"Successfully added configuration {configSetIdentifier}");
            }
            catch (ConfigurationSetAlreadyExistsException)
            {
                Console.WriteLine($"Config {configSetIdentifier} already exists.");
            }
            finally
            {
                clientSession.Session.UseLoggingConfigurationSet(config.Identifier);
            }
        }

        public void AddBasicEventConfiguration(IClientSession clientSession, List<string> eventIdentifiers)
        {
            var configSetIdentifier = Guid.NewGuid().ToString();
            var configSetManager = new ConfigurationSetManager();
            var config = configSetManager.Create(connectionString, configSetIdentifier, "");
            foreach (string eventIdentifier in eventIdentifiers)
            {
                config.AddConversion(defaultConversion);
                var parameterGroup = new ParameterGroup("Stream API");
                config.AddParameterGroup(parameterGroup);
                var applicationGroup = new ApplicationGroup("Stream API", "Stream API", new List<string>() { "Stream API" });
                applicationGroup.SupportsRda = false;
                config.AddGroup(applicationGroup);
                var eventDefId = Random.Shared.Next(0, 600);
                var eventDefinitionSql = new EventDefinition(
                    eventDefId,
                    eventIdentifier,
                    EventPriorityType.Low,
                    new List<string>() { "DefaultConversion", "DefaultConversion", "DefaultConversion" },
                    "Stream API"
                );
                config.AddEventDefinition(eventDefinitionSql);
                eventDefCache[eventIdentifier] = eventDefinitionSql;
            }
            try
            {
                config.Commit();
                Console.WriteLine($"Successfully added configuration {configSetIdentifier}");
            }
            catch (ConfigurationSetAlreadyExistsException)
            {
                Console.WriteLine($"Config {configSetIdentifier} already exists.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to add config due to {ex.Message}");
            }
            finally
            {
                clientSession.Session.UseLoggingConfigurationSet(config.Identifier);
            }
        }

        public void AddBasicEventConfiguration(IClientSession clientSession, string eventIdentifier)
        {
            var configSetIdentifier = Guid.NewGuid().ToString();
            var configSetManager = new ConfigurationSetManager();
            var config = configSetManager.Create(connectionString, configSetIdentifier, "");
            config.AddConversion(defaultConversion);
            var parameterGroup = new ParameterGroup("Stream API");
            config.AddParameterGroup(parameterGroup);
            var applicationGroup = new ApplicationGroup("Stream API", "Stream API", new List<string>() { "Stream API" });
            applicationGroup.SupportsRda = false;
            config.AddGroup(applicationGroup);
            var eventDefId = (int)Random.Shared.Next(0, 600);
            var eventDefinitionSql = new EventDefinition(
                eventDefId,
                eventIdentifier,
                EventPriorityType.Low,
                new List<string>() { "DefaultConversion", "DefaultConversion", "DefaultConversion" },
                "Stream API"
            );
            config.AddEventDefinition(eventDefinitionSql);
            eventDefCache[eventIdentifier] = eventDefinitionSql;
            try
            {
                config.Commit();
                //clientSession.Session.LoadConfiguration();
                Console.WriteLine($"Successfully added configuration {configSetIdentifier}");
            }
            catch (ConfigurationSetAlreadyExistsException)
            {
                Console.WriteLine($"Config {configSetIdentifier} already exists.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to add config due to {ex.Message}");
            }
            finally
            {
                clientSession.Session.UseLoggingConfigurationSet(config.Identifier);
            }
        }

        public bool TryAddData(IClientSession clientSession, string parameterIdentifier, List<double> data,
            List<long> timestamps)
        {
            if (!channelIdParameterDictionary.ContainsKey(parameterIdentifier))
            {
                AddBasicConfiguration(clientSession, parameterIdentifier);
            }
            var channelId = channelIdParameterDictionary[parameterIdentifier];

            var dataBytes = new byte[data.Count * sizeof(double)];
            var index = 0;
            dataBytes = data.SelectMany(BitConverter.GetBytes).ToArray();

            clientSession.Session.AddRowData(channelId, timestamps.ToArray(), dataBytes, 8, false);

            return true;
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

            clientSession.Session.LapCollection.Add(newLap);

            Console.WriteLine($"Added lap {lapName} at {timestamp}");
        }

        public void CloseSession(IClientSession clientSession)
        {
            var identifier = clientSession.Session.Identifier;
            if (clientSession != null)
            {
                clientSession.Session.EndData();
                clientSession.Close();
                Console.WriteLine($"Closed Session {identifier}.");
            }
        }

        public void AddDetails(IClientSession clientSession, string key, string value)
        {
            var sessionDetailItem = new SessionDataItem(key, value);
            clientSession.Session.Items.Add(sessionDetailItem);
        }

        public void AddMarker(IClientSession clientSession, long timestamp, string label)
        {
            var marker = new Marker(timestamp, label);
            clientSession.Session.Markers.Add(marker);
        }

        public void AddEvent(IClientSession clientSession, string eventIdentifier, long timestamp, IList<double> data,
            string groupName = "Stream API")
        {
            clientSession.Session.LoadConfiguration();
            clientSession.Session.Events.AddEventData(eventDefCache[eventIdentifier].EventDefinitionId, groupName, timestamp, data);
        }

        public void UpdateSessionInfo(IClientSession clientSession, GetSessionInfoResponse sessionInfo)
        {
            clientSession.Session.UpdateIdentifier(sessionInfo.Identifier);
        }
    }
}