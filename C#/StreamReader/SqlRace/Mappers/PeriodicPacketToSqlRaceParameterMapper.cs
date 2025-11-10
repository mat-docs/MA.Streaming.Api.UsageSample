// <copyright file="PeriodicPacketToSqlRaceParameterMapper.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

using MA.Streaming.OpenData;

using Stream.Api.Stream.Reader.Abstractions;
using Stream.Api.Stream.Reader.Dto;

namespace Stream.Api.Stream.Reader.SqlRace.Mappers
{
    internal class PeriodicPacketToSqlRaceParameterMapper : BaseMapper
    {
        public PeriodicPacketToSqlRaceParameterMapper(SessionConfig sessionConfig)
            : base(sessionConfig)
        {
        }

        public IReadOnlyList<ISqlRaceDto> MapParameter(PeriodicDataPacket packet, IReadOnlyList<string> parameterList)
        {
            var mappedParameters = new List<SqlRacePeriodicDto>();
            for (var i = 0; i < parameterList.Count; i++)
            {
                var parameterIdentifier = parameterList[i];
                var column = packet.Columns[i];
                switch (column.ListCase)
                {
                    case SampleColumn.ListOneofCase.DoubleSamples:
                    {
                        mappedParameters.Add(this.CreateSqlRaceDto(column.DoubleSamples.Samples.Select(x => x.Value).ToList(), parameterIdentifier, packet));
                        break;
                    }
                    case SampleColumn.ListOneofCase.Int32Samples:
                    {
                        mappedParameters.Add(this.CreateSqlRaceDto(column.Int32Samples.Samples.Select(x => (double)x.Value).ToList(), parameterIdentifier, packet));
                        break;
                    }
                    case SampleColumn.ListOneofCase.BoolSamples:
                    {
                        mappedParameters.Add(this.CreateSqlRaceDto(column.BoolSamples.Samples.Select(x => x.Value ? 1.0 : 0.0).ToList(), parameterIdentifier, packet));
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
            }

            return mappedParameters;
        }

        private SqlRacePeriodicDto CreateSqlRaceDto(List<double> samples, string parameterIdentifier, PeriodicDataPacket packet)
        {
            var data = samples.SelectMany(BitConverter.GetBytes).ToArray();
            return new SqlRacePeriodicDto(
                [this.SessionConfig.GetParameterChannelId(parameterIdentifier, packet.Interval)],
                ConvertUnixToSqlRaceTime(packet.StartTime),
                data,
                packet.Interval,
                samples.Count);
        }
    }
}