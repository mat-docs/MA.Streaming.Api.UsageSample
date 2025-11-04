// <copyright file="RowPacketToSqlRaceParameterMapper.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

using MA.Streaming.OpenData;

using Stream.Api.Stream.Reader.Abstractions;
using Stream.Api.Stream.Reader.Dto;

namespace Stream.Api.Stream.Reader.SqlRace.Mappers
{
    internal class RowPacketToSqlRaceParameterMapper : BaseMapper
    {
        public RowPacketToSqlRaceParameterMapper(SessionConfig sessionConfig)
            : base(sessionConfig)
        {
        }

        public IReadOnlyList<ISqlRaceDto> MapParameter(RowDataPacket packet, IReadOnlyList<string> parameterList)
        {
            var mappedParameters = new List<ISqlRaceDto>();
            var channels = parameterList.Select(this.SessionConfig.GetParameterChannelId).ToList();
            for (var i = 0; i < packet.Rows.Count; i++)
            {
                var row = packet.Rows[i];
                var timestamp = packet.Timestamps[i];
                switch (row.ListCase)
                {
                    case SampleRow.ListOneofCase.DoubleSamples:
                    {
                        mappedParameters.Add(this.CreateSqlRowDto(channels, row.DoubleSamples.Samples.Select(x => x.Value), timestamp));
                        break;
                    }
                    case SampleRow.ListOneofCase.Int32Samples:
                    {
                        mappedParameters.Add(this.CreateSqlRowDto(channels, row.Int32Samples.Samples.Select(x => (double)x.Value), timestamp));
                        break;
                    }
                    case SampleRow.ListOneofCase.BoolSamples:
                    {
                        mappedParameters.Add(this.CreateSqlRowDto(channels, row.BoolSamples.Samples.Select(x => x.Value ? 1.0 : 0.0), timestamp));
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
            }

            return mappedParameters;
        }

        private SqlRaceRowDto CreateSqlRowDto(IReadOnlyList<uint> channels, IEnumerable<double> samples, ulong timestamp)
        {
            return new SqlRaceRowDto(channels, timestamp.ToSqlRaceTime(), samples.SelectMany(BitConverter.GetBytes).ToArray(), 0);
        }
    }
}