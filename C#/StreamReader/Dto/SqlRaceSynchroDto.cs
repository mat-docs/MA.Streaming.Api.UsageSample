// <copyright file="SqlRaceSynchroDto.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

using Stream.Api.Stream.Reader.Abstractions;

namespace Stream.Api.Stream.Reader.Dto
{
    public class SqlRaceSynchroDto : ISqlRaceDto, ISqlRaceSynchroDto
    {
        public SqlRaceSynchroDto(uint channelId, long timestamp, byte numberOfSamples, uint deltaScale, byte[] data)
        {
            this.DataType = "Synchro";
            this.NumberOfSamples = numberOfSamples;
            this.DeltaScale = deltaScale;
            this.Data = data;
            this.ChannelId = channelId;
            this.Timestamp = timestamp;
        }

        public string DataType { get; }

        public uint ChannelId { get; }

        public long Timestamp { get; }

        public byte NumberOfSamples { get; }

        public uint DeltaScale { get; }

        public byte[] Data { get; }
    }
}