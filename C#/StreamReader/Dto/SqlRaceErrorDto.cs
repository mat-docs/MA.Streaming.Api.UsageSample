// <copyright file="SqlRaceErrorDto.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

using MA.Streaming.OpenData;

using Stream.Api.Stream.Reader.Abstractions;

namespace Stream.Api.Stream.Reader.Dto
{
    public class SqlRaceErrorDto : ISqlRaceDto, ISqlRaceErrorDto
    {
        public SqlRaceErrorDto(
            string errorIdentifier,
            string currentParameterIdentifier,
            uint currentParameterChannel,
            string loggedParameterIdentifier,
            uint loggedParameterChannel,
            long timestamp,
            ErrorType errorType,
            ErrorStatus errorStatus)
        {
            this.DataType = "Error";
            this.ErrorIdentifier = errorIdentifier;
            this.CurrentParameterIdentifier = currentParameterIdentifier;
            this.LoggedParameterIdentifier = loggedParameterIdentifier;
            this.ErrorType = errorType;
            this.ErrorStatus = errorStatus;
            this.CurrentParameterChannel = currentParameterChannel;
            this.LoggedParameterChannel = loggedParameterChannel;
            this.Timestamp = timestamp;
        }

        public string DataType { get; }

        public string ErrorIdentifier { get; }

        public string CurrentParameterIdentifier { get; }

        public uint CurrentParameterChannel { get; }

        public string LoggedParameterIdentifier { get; }

        public uint LoggedParameterChannel { get; }

        public long Timestamp { get; }

        public ErrorType ErrorType { get; }

        public ErrorStatus ErrorStatus { get; }
    }
}