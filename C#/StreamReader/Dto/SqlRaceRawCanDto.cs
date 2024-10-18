// <copyright file="SqlRaceRawCanDto.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MA.Streaming.OpenData;

using Stream.Api.Stream.Reader.Abstractions;

namespace Stream.Api.Stream.Reader.Dto
{
    public class SqlRaceRawCanDto : ISqlRaceDto, ISqlRaceRawCanDto
    {
        public SqlRaceRawCanDto(long timestamp, ushort canBus, uint canId, byte[] payload,byte canType)
        {
            this.DataType = "Raw Can";
            this.Timestamp = timestamp;
            this.CanBus = canBus;
            this.CanId = canId;
            this.Payload = payload;
            this.CanType = canType;
        }

        public string DataType { get; }

        public long Timestamp { get; }

        public ushort CanBus { get; }

        public uint CanId { get; }

        public byte[] Payload { get; }

        public byte CanType { get; }
    }
}