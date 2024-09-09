// <copyright file="RowPacketToSqlRaceParameterMapper.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MA.Streaming.OpenData;

using Stream.Api.Stream.Reader.Abstractions;
using Stream.Api.Stream.Reader.Dto;

namespace Stream.Api.Stream.Reader.SqlRace.Mappers
{
    public class RowPacketToSqlRaceParameterMapper : BaseMapper
    {
        public RowPacketToSqlRaceParameterMapper(SessionConfig sessionConfig)
        {
            this.SessionConfig = sessionConfig;
        }

        public IReadOnlyList<ISqlRaceDto> MapParameter(RowDataPacket packet, IReadOnlyList<string> parameterList)
        {
            var mappedParameters = new List<ISqlRaceDto>();
            var channels = parameterList.Select(this.SessionConfig.GetParameterChannelId).ToList();
            for (var i = 0; i < packet.Rows.Count; i++)
            {
                var samples = new List<double>();
                var row = packet.Rows[i];
                var timestamp = packet.Timestamps[i];
                switch (row.ListCase)
                {
                    case SampleRow.ListOneofCase.DoubleSamples:
                    {
                        samples = row.DoubleSamples.Samples.Select(x => x.Value).ToList();
                        break;
                    }
                    case SampleRow.ListOneofCase.Int32Samples:
                    {
                        samples = row.Int32Samples.Samples.Select(x => (double)x.Value).ToList();
                        break;
                    }
                    case SampleRow.ListOneofCase.BoolSamples:
                    {
                        samples = row.BoolSamples.Samples.Select(x => x.Value ? 1.0 : 0.0).ToList();
                        break;
                    }
                    case SampleRow.ListOneofCase.StringSamples:
                    {
                        Console.WriteLine("Unable to add String data for this row.");
                        continue;
                    }
                    case SampleRow.ListOneofCase.None:
                    default:
                    {
                        Console.WriteLine("No rows found for this packet.");
                        continue;
                    }
                }

                mappedParameters.Add(
                    new SqlRaceRowDto
                    {
                        Channels = channels,
                        Data = samples.SelectMany(BitConverter.GetBytes).ToArray(),
                        Interval = 0,
                        Timestamp = ConvertUnixToSqlRaceTime(timestamp)
                    });
            }

            return mappedParameters;
        }
    }
}