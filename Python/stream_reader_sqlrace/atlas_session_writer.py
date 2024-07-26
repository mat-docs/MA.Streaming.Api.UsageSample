import os
import logging
import struct
import datetime
from typing import List

from ma.streaming.open_data.v1 import open_data_pb2
from stream_reader_sqlrace.sql_race import SQLiteConnection

logger = logging.getLogger(__name__)
A10_INSTALL_PATH = r"C:\Program Files\McLaren Applied Technologies\ATLAS 10"
SQL_RACE_DLL_PATH = rf"{A10_INSTALL_PATH}\MESL.SqlRace.Domain.dll"
SQL_RACE_RUNTIME_CONFIG = rf"{A10_INSTALL_PATH}\MAT.Atlas.Host.runtimeconfig.json"

# configure pythonnet runtime for SQLRace API
os.environ["PYTHONNET_RUNTIME"] = "coreclr"
os.environ["PYTHONNET_CORECLR_RUNTIME_CONFIG"] = SQL_RACE_RUNTIME_CONFIG

# only import clr after the runtime has been configured, so pylint: disable=wrong-import-position
import clr

# Configure Pythonnet and reference the required assemblies for dotnet and SQL Race
clr.AddReference("System.Collections")  # pylint: disable=no-member
clr.AddReference("System.Core")  # pylint: disable=no-member
clr.AddReference("System.IO")  # pylint: disable=no-member

if not os.path.isfile(SQL_RACE_DLL_PATH):
    raise FileNotFoundError(
        f"Couldn't find SQL Race DLL at {SQL_RACE_DLL_PATH} please check that Atlas 10 "
        f"is installed."
    )

clr.AddReference(SQL_RACE_DLL_PATH)  # pylint: disable=no-member

from System.Collections.Generic import (  # .NET imports, so pylint: disable=wrong-import-position,wrong-import-order,import-error
    List as NETList,
)

from System import (  # .NET imports, so pylint: disable=wrong-import-position,wrong-import-order,import-error
    Byte,
    String,
    UInt32,
    Array,
    Int64,
    Double,
)
from MESL.SqlRace.Domain import (  # .NET imports, so pylint: disable=wrong-import-position,wrong-import-order,import-error
    Lap,
    ConfigurationSetManager,
    ParameterGroup,
    ApplicationGroup,
    RationalConversion,
    TextConversion,
    ConfigurationSetAlreadyExistsException,
    Parameter,
    Channel,
    DatabaseConnectionInformation,
    SessionDataItem,
    Marker,
    EventDefinition,
)
from MESL.SqlRace.Enumerators import (  # .NET imports, so pylint: disable=wrong-import-position,wrong-import-order,import-error
    DataType,
    ChannelDataSourceType,
    EventPriorityType,
)


class AtlasSessionWriter:

    def __init__(self, db_location: str = r"C:\McLaren Applied\StreamAPIDemo.ssndb"):
        self.sql_race_connection = None
        self.session = None
        self.parameter_channel_id_mapping = {}
        self.create_sqlrace_session(db_location)

    def create_sqlrace_session(self, db_location: str):
        sql_race_connection = SQLiteConnection(
            db_location,
            session_identifier=f"Stream API DEMO {datetime.datetime.now()}",
            mode="w",
            recorder=True,
        )
        self.sql_race_connection = sql_race_connection
        self.session = sql_race_connection.session

    def add_configration(self, packet: open_data_pb2.ConfigurationPacket):
        """Creates parameters in ATLAS Session from the configuration

        This is a simplified implementation of the SQLRace API and will throw away a
        large amount of information. For all parameters regardless of the data type
        specified, it will create a row channel of doubles.

        This function also ignores all the groups.

        For examples to use the SQLRace API fully, see the SQLRace API code samples.

        Args:
            packet: Configuration Packet received from the Stream API.

        Returns:
            None, config is created and committed to the session.
        """
        logger.debug("Creating new config.")
        config_identifier = packet.config_id
        config_description = "Stream API generated config"
        configSetManager = (  # .NET objects, so pylint: disable=invalid-name
            ConfigurationSetManager.CreateConfigurationSetManager()
        )

        # if we have processed this config previously then we can just use it
        if configSetManager.Exists(
            DatabaseConnectionInformation(self.session.ConnectionString),
            config_identifier,
        ):
            logger.info(
                "Logging config already exist, skip reprocessing logging config. "
                "Config identifier: %s",
                config_identifier,
            )
            self.session.UseLoggingConfigurationSet(config_identifier)
            return

        config = configSetManager.Create(
            self.session.ConnectionString, config_identifier, config_description
        )

        # Create 1to1 conversion
        one_to_one_conversion_name = "DefaultConversion"
        config.AddConversion(
            RationalConversion.CreateSimple1To1Conversion(
                one_to_one_conversion_name, "", "%5.2f"
            )
        )

        app_groups = set()

        # find all application groups
        for parameter_definition in packet.parameter_definitions:
            app_groups.add(parameter_definition.application_name)

        # add applications and parameter group for all the app groups
        for app in app_groups:
            group1 = ParameterGroup(app, app)
            config.AddParameterGroup(group1)
            # .NET objects, so pylint: disable=invalid-name
            parameterGroupIds = NETList[String]()
            parameterGroupIds.Add(group1.Identifier)
            # .NET objects, so pylint: disable=invalid-name
            applicationGroup = ApplicationGroup(
                app,
                parameterGroupIds,
            )
            applicationGroup.SupportsRda = False
            config.AddGroup(applicationGroup)

        # Add a row channel per parameter
        for parameter_definition in packet.parameter_definitions:
            conversion = one_to_one_conversion_name
            # Add a row channel
            channel_id = self.session.ReserveNextAvailableRowChannelId() % 2147483647
            self.parameter_channel_id_mapping[parameter_definition.identifier] = (
                channel_id
            )
            # .NET objects, so pylint: disable=invalid-name
            myParameterChannel = Channel(
                channel_id,
                "MyParamChannel",
                0,
                DataType.Double64Bit,
                ChannelDataSourceType.RowData,
            )
            config.AddChannel(myParameterChannel)

            # If text conversion definition exist, then create the text conversion and
            # update the conversion identifier from the default
            if parameter_definition.conversion.conversion_identifier != "":
                config.AddConversion(
                    TextConversion(
                        parameter_definition.conversion.conversion_identifier,
                        parameter_definition.units,
                        parameter_definition.format_string,
                        parameter_definition.conversion.input_values,
                        parameter_definition.conversion.string_values,
                        parameter_definition.conversion.default,
                    )
                )
                conversion = parameter_definition.conversion.conversion_identifier

            # Add Parameter
            # .NET objects, so pylint: disable=invalid-name
            myParamChannelId = NETList[UInt32]()
            myParamChannelId.Add(channel_id)

            # .NET objects, so pylint: disable=invalid-name
            parameterGroupIdentifiers = NETList[String]()
            parameterGroupIdentifiers.Add(parameter_definition.application_name)

            # .NET objects, so pylint: disable=invalid-name
            myParameter = Parameter(
                parameter_definition.identifier,
                parameter_definition.name,
                parameter_definition.description,
                parameter_definition.max_value,
                parameter_definition.min_value,
                parameter_definition.warning_max_value,
                parameter_definition.warning_min_value,
                0.0,
                0xFFFF,
                0,
                conversion,
                parameterGroupIdentifiers,
                myParamChannelId,
                parameter_definition.application_name,
                parameter_definition.format_string,
                parameter_definition.units,
            )
            config.AddParameter(myParameter)

        for event_definition in packet.event_definitions:
            event_def_priority_map = {
                open_data_pb2.EVENT_PRIORITY_DEBUG: EventPriorityType.Debug,
                open_data_pb2.EVENT_PRIORITY_LOW: EventPriorityType.Low,
                open_data_pb2.EVENT_PRIORITY_MEDIUM: EventPriorityType.Medium,
                open_data_pb2.EVENT_PRIORITY_HIGH: EventPriorityType.High,
                # no critical level in SQLRace, map it to high
                open_data_pb2.EVENT_PRIORITY_CRITICAL: EventPriorityType.High,
                # no unspecified level in SQLRace, map it to low
                open_data_pb2.EVENT_PRIORITY_UNSPECIFIED: EventPriorityType.Low,
            }

            # Process the text configuration, if any.
            # If there are no text configuration found then the one to one configuration
            # will be applied.
            conversion_function_names = [one_to_one_conversion_name] * 3
            for i, text_conversion_definition in enumerate(
                event_definition.conversions
            ):
                if text_conversion_definition.conversion_identifier != "":
                    config.AddConversion(
                        TextConversion(
                            text_conversion_definition.conversion_identifier,
                            "",
                            "5.2f",
                            text_conversion_definition.input_values,
                            text_conversion_definition.string_values,
                            text_conversion_definition.default,
                        )
                    )
                    conversion_function_names[i] = (
                        text_conversion_definition.conversion_identifier
                    )
            # .NET objects, so pylint: disable=invalid-name
            conversionFunctionNames = Array[String](conversion_function_names)

            # .NET objects, so pylint: disable=invalid-name
            eventDefinition = EventDefinition(
                event_definition.definition_id,
                event_definition.description,
                event_def_priority_map[event_definition.priority],
                conversionFunctionNames,
                event_definition.application_name,
            )

            config.AddEventDefinition(eventDefinition)

        try:
            config.Commit()
            logger.debug("Config committed, id: %s", config.Identifier)
        except ConfigurationSetAlreadyExistsException:
            logger.warning(
                "Cannot commit config %s, config already exist.", config.Identifier
            )
        self.session.UseLoggingConfigurationSet(config.Identifier)

    def add_data(
        self, parameter_identifier: str, data: List[float], timestamps: List[float]
    ) -> bool:
        """Add data to a parameter.

        Data are in floats, and timestamp as in seconds from midnight (SQLRace format)
        If the config for the parameter was not processed beforehand data will not be
        added.

        Args:
            parameter_identifier: Parameter identifier to add the data to
            data: List of data
            timestamps: List of timestamp in SQLRace format

        Returns:
            True if the config is found and data added.
        """
        try:
            channel_id = self.parameter_channel_id_mapping[parameter_identifier]
        except KeyError:
            logger.warning(
                "No config processed for parameter %s, data not added",
                parameter_identifier,
            )
            return False
        channelIds = NETList[UInt32]()  # .NET objects, so pylint: disable=invalid-name
        channelIds.Add(channel_id)

        databytes = bytearray(len(data) * 8)
        for i, value in enumerate(data):
            new_bytes = struct.pack("d", value)
            databytes[i * 8 : i * 8 + len(new_bytes)] = new_bytes

        timestamps_array = Array[Int64](len(timestamps))
        for i, timestamp in enumerate(timestamps):
            timestamps_array[i] = Int64(int(timestamp))

        self.session.AddRowData(channel_id, timestamps_array, databytes, 8, False)

        # if there isn't a lap then at one at the start
        if self.session.LapCollection.Count == 0:
            self.add_lap(min(timestamps))

        return True

    def add_lap(
        self,
        timestamp: int,
        lap_number: int = 1,
        lap_name: str = "Lap 1",
        count_for_fastest_lap: bool = True,
    ) -> None:
        """Add a new lap to the session.

        Args:
            timestamp: Timestamp to add the lap.
            lap_number: Lap number. Default to be `Session.LapCollection.Count + 1`
            lap_name: Lap name. Default to be "Lap {lap_number}".
            count_for_fastest_lap: True if the lap should be considered as part of the
                fastest lap calculation (e.g. a timed lap). Default to be True.

        Returns:
            None
        """
        newlap = Lap(
            int(timestamp),
            lap_number,
            Byte(0),
            lap_name,
            count_for_fastest_lap,
        )
        self.session.LapCollection.Add(newlap)
        logger.info(
            'Lap "%s" with number %i added at %s', lap_name, lap_number, timestamp
        )

    def close_session(self):
        # Close the session if one was created
        if self.sql_race_connection is not None:
            self.sql_race_connection.close_session()

    def add_details(self, key, value):
        """Add a session detail to the session."""
        session_item = SessionDataItem(key, value)
        self.session.Items.Add(session_item)

    def add_marker(self, timestamp: int, label: str):
        """Add a point marker to the session."""
        new_point_marker = Marker(int(timestamp), label)
        new_markers = Array[Marker]([new_point_marker])
        self.session.Markers.Add(new_markers)

    def add_event_data(self, event_definition_key, event_time, raw_data):
        """Add an event instance data to the session."""
        raw_data = Array[Double](raw_data)
        self.session.Events.AddEventData(
            event_definition_key, "", event_time, raw_data  # default to no group name
        )
