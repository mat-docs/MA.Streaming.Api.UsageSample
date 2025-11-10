// <copyright file="SynchroPacketToSqlRaceSynchroMapper.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

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
                        mappedParameters.Add(CreateSqlRaceDto(samples, packet.Intervals, channelId, deltaScale, packet.StartTime));
                        break;
                    }
                    case SampleColumn.ListOneofCase.Int32Samples:
                    {
                        var samples = column.Int32Samples.Samples.Select(x => (double)x.Value).ToList();
                        mappedParameters.Add(CreateSqlRaceDto(samples, packet.Intervals, channelId, deltaScale, packet.StartTime));
                        break;
                    }
                    case SampleColumn.ListOneofCase.BoolSamples:
                    {
                        var samples = column.BoolSamples.Samples.Select(x => x.Value ? 1.0 : 0.0).ToList();
                        mappedParameters.Add(CreateSqlRaceDto(samples, packet.Intervals, channelId, deltaScale, packet.StartTime));
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

        private static ISqlRaceDto CreateSqlRaceDto(IReadOnlyList<double> samples, IReadOnlyList<uint> intervals, uint channelId, uint deltaScale, ulong timestamp)
        {
            var data = CreateSqlRaceDataList(samples, intervals, deltaScale);
            var sampleCount = byte.Parse(samples.Count.ToString());
            return new SqlRaceSynchroDto(channelId, timestamp.ToSqlRaceTime(), sampleCount, deltaScale, data);
        }

        private static byte[] CreateSqlRaceDataList(IReadOnlyList<double> samples, IReadOnlyList<uint> intervals, uint deltaScale)
        {
            var totalItems = samples.Count + intervals.Count;
            var dataIntervalList = new List<byte>();
            var pairing = 0;
            for (var i = 0; i < totalItems; i++)
            {
                if (i % 2 == 0)
                {
                    dataIntervalList.AddRange(BitConverter.GetBytes(samples[pairing]));
                }
                else
                {
                    var interval = intervals[pairing] / deltaScale;
                    dataIntervalList.AddRange(BitConverter.GetBytes((ushort)interval));
                    pairing++;
                }
            }

            return [.. dataIntervalList];
        }

        private static uint GetDeltaScale(IReadOnlyList<uint> intervals)
        {
            var deltaScale = intervals[0];
            for (var i = 1; i < intervals.Count; i++)
            {
                deltaScale = GCD(deltaScale, intervals[i]);
            }

            return deltaScale;
        }

        private static uint GCD(uint a, uint b)
        {
            while (b != 0)
            {
                var temp = b;
                b = a % b;
                a = temp;
            }

            return a;
        }
    }
}