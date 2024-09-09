// <copyright file="PeriodicPacketToSqlRaceParameterMapper.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MA.Streaming.OpenData;

using Stream.Api.Stream.Reader.Abstractions;
using Stream.Api.Stream.Reader.Dto;

namespace Stream.Api.Stream.Reader.SqlRace.Mappers
{
    public class PeriodicPacketToSqlRaceParameterMapper : BaseMapper
    {
        public PeriodicPacketToSqlRaceParameterMapper(SessionConfig sessionConfig)
        {
            this.SessionConfig = sessionConfig;
        }

        public IReadOnlyList<ISqlRaceDto> MapParameter(PeriodicDataPacket packet, IReadOnlyList<string> parameterList)
        {
            var mappedParameters = new List<SqlRacePeriodicDto>();
            for (var i = 0; i < parameterList.Count; i++)
            {
                var parameterIdentifier = parameterList[i];
                var column = packet.Columns[i];
                var samples = new List<double>();
                switch (column.ListCase)
                {
                    case SampleColumn.ListOneofCase.DoubleSamples:
                    {
                        samples = column.DoubleSamples.Samples.Select(x => x.Value).ToList();
                        break;
                    }
                    case SampleColumn.ListOneofCase.Int32Samples:
                    {
                        samples = column.Int32Samples.Samples.Select(x => (double)x.Value).ToList();
                        break;
                    }
                    case SampleColumn.ListOneofCase.BoolSamples:
                    {
                        samples = column.BoolSamples.Samples.Select(x => x.Value ? 1.0 : 0.0).ToList();
                        break;
                    }
                    case SampleColumn.ListOneofCase.StringSamples:
                    {
                        Console.WriteLine($"Unable to decode String Parameter {parameterIdentifier}");
                        continue;
                    }
                    case SampleColumn.ListOneofCase.None:
                    default:
                    {
                        Console.WriteLine($"No data found for parameter {parameterIdentifier}.");
                        continue;
                    }
                }

                var data = samples.SelectMany(BitConverter.GetBytes).ToArray();
                mappedParameters.Add(
                    new SqlRacePeriodicDto
                    {
                        Channels = [this.SessionConfig.GetParameterChannelId(parameterIdentifier, packet.Interval)],
                        Count = samples.Count,
                        Data = data,
                        Interval = packet.Interval,
                        Timestamp = ConvertUnixToSqlRaceTime(packet.StartTime)
                    });
            }

            return mappedParameters;
        }
    }
}