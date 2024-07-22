"""Code to build Packets according to the Open Data specification."""

import time
from typing import List
import numpy as np

from ma.streaming.open_data.v1 import open_data_pb2


def construct_double_column(
    samples: List[float],
    data_status: open_data_pb2.DataStatus = open_data_pb2.DataStatus.DATA_STATUS_VALID,
) -> open_data_pb2.SampleColumn:
    """Construct a DoubleColum from a list of doubles

    Args:
        samples: List of sample values
        data_status: One of open_data_pb2.DataStatus, default to be
            open_data_pb2.DataStatus.DATA_STATUS_VALID

    Returns:
        A DoubleColum object containing DoubleSample with the sample values and data
        status.
    """
    sample_class = open_data_pb2.DoubleSample
    constructed_samples = [
        sample_class(value=sample, status=data_status) for sample in samples
    ]
    sample_column = open_data_pb2.SampleColumn(
        double_samples=open_data_pb2.DoubleSampleList(samples=constructed_samples)
    )
    return sample_column


class SinWaveGenerator:
    """Generate the relevant packets for a sin wave.

    Attributes:
        time_ns: Time in ns since UNIX epoch.
        frequency: Frequency of the samples generated, in Hz.
        param_identifiers: Two parameter identifier that corresponds to the sin and cos
            waves.
    """

    def __init__(self):
        self.time_ns = time.time_ns()
        self.frequency = 1000  # Hz
        self.param_identifiers = ["Sin:MyApp", "Cos:MyApp"]

    @property
    def interval(self):
        """Interval between samples in ns."""
        return int(1e9 / self.frequency)

    def get_new_packet(self, data_format_id: int):
        """Generate the PeriodicDataPacket for the next segment of the sine wave."""
        t = self.time_ns

        sin_values = [np.sin((t + i) / 1e9) for i in np.arange(0, 1e9, self.interval)]
        cos_values = [np.cos((t + i) / 1e9) for i in np.arange(0, 1e9, self.interval)]
        self.time_ns = t + int(1e9)
        sin_column = construct_double_column(sin_values)
        cos_column = construct_double_column(cos_values)
        data_format = open_data_pb2.SampleDataFormat(
            data_format_identifier=data_format_id,
        )
        data_packet = open_data_pb2.PeriodicDataPacket(
            data_format=data_format,
            start_time=t,
            interval=self.interval,
            columns=[sin_column, cos_column],
        )
        return data_packet

    def build_configuration_packet(self, config_id: str):
        parameter_definitions = []
        for param_id in self.param_identifiers:
            param_def = self.build_parameter_definition_packet(*param_id.split(":"))
            parameter_definitions.append(param_def)
        config_packet = open_data_pb2.ConfigurationPacket(
            config_id=config_id,
            parameter_definitions=parameter_definitions,
        )
        return config_packet

    def build_parameter_definition_packet(self, name: str, app: str):
        param_def = open_data_pb2.ParameterDefinition(
            identifier=f"{name}:{app}",
            name=name,
            application_name=app,
            description="",
            groups=[""],
            units="m",
            data_type=open_data_pb2.DATA_TYPE_FLOAT64,
            format_string="6.3f",
            frequencies=[self.frequency],
            max_value=1,
            min_value=-1,
        )
        return param_def
