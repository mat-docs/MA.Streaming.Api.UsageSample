// <copyright file="SynchroPacketToSqlRaceSynchroMapper.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using System.Numerics;

using MA.Streaming.OpenData;

using Stream.Api.Stream.Reader.Abstractions;
using Stream.Api.Stream.Reader.Dto;

namespace Stream.Api.Stream.Reader.SqlRace.Mappers
{
    internal class SynchroPacketToSqlRaceSynchroMapper : BaseMapper
    {
        public SynchroPacketToSqlRaceSynchroMapper(SessionConfig sessionConfig)
            : base(sessionConfig)
        {
        }

        public IReadOnlyList<ISqlRaceDto> MapParameter(SynchroDataPacket packet, IReadOnlyList<string> parameterList)
        {
            var mappedParameters = new List<ISqlRaceDto>();
            for (var i = 0; i < packet.Column.Count; i++)
            {
                var column = packet.Column[i];
                var parameter = parameterList[i];
                var channelId = this.SessionConfig.GetSynchroChannelId(parameter);
                var deltaScale = GetDeltaScale(packet.Intervals);
                switch (column.ListCase)
                {
                    case SampleColumn.ListOneofCase.DoubleSamples:
                    {
                        var samples = column.DoubleSamples.Samples.Select(x => x.Value).ToList();
                        var sampleCount = byte.Parse(samples.Count.ToString());
                        var data = CreateSqlRaceDataList(samples, packet.Intervals);
                        mappedParameters.Add(CreateSqlRaceDto(data, channelId, deltaScale, packet.StartTime, sampleCount));
                        break;
                    }
                    case SampleColumn.ListOneofCase.Int32Samples:
                    {
                        var samples = column.Int32Samples.Samples.Select(x => (double)x.Value).ToList();
                        var sampleCount = byte.Parse(samples.Count.ToString());
                        var data = CreateSqlRaceDataList(samples, packet.Intervals);
                        mappedParameters.Add(CreateSqlRaceDto(data, channelId, deltaScale, packet.StartTime, sampleCount));
                        break;
                    }
                    case SampleColumn.ListOneofCase.BoolSamples:
                    {
                        var samples = column.BoolSamples.Samples.Select(x => x.Value ? 1.0 : 0.0).ToList();
                        var sampleCount = byte.Parse(samples.Count.ToString());
                        var data = CreateSqlRaceDataList(samples, packet.Intervals);
                        mappedParameters.Add(CreateSqlRaceDto(data, channelId, deltaScale, packet.StartTime, sampleCount));
                        break;
                    }
                    case SampleColumn.ListOneofCase.None:
                    case SampleColumn.ListOneofCase.StringSamples:
                    default:
                    {
                        Console.WriteLine($"Unable to map Synchro packet for {parameter}.");
                        break;
                    }
                }
            }

            return mappedParameters;
        }

        private static ISqlRaceDto CreateSqlRaceDto(byte[] data, uint channelId, byte deltaScale, ulong timestamp, byte sampleCount)
        {
            return new SqlRaceSynchroDto(channelId, timestamp.ToSqlRaceTime(), sampleCount, deltaScale, data);
        }

        private static byte[] CreateSqlRaceDataList(IReadOnlyList<double> samples, IReadOnlyList<uint> intervals)
        {
            var dataIntervalList = new List<double>();
            var totalItems = samples.Count + intervals.Count;
            var pairing = 0;
            for (var i = 0; i < totalItems; i++)
            {
                if (i % 2 == 0)
                {
                    dataIntervalList.Add(samples[pairing]);
                }
                else
                {
                    dataIntervalList.Add(intervals[pairing]);
                    pairing++;
                }
            }

            return dataIntervalList.SelectMany(BitConverter.GetBytes).ToArray();
        }

        private static byte GetDeltaScale(IReadOnlyList<uint> intervals)
        {
            var deltaScale = BigInteger.GreatestCommonDivisor(BigInteger.Parse(intervals[0].ToString()), BigInteger.Parse(intervals[1].ToString())).ToString();
            return byte.Parse(deltaScale);
        }
    }
}