// <copyright file="ISqlRaceErrorDto.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

using MA.Streaming.OpenData;

namespace Stream.Api.Stream.Reader.Abstractions;

public interface ISqlRaceErrorDto
{
    string ErrorIdentifier { get; }

    string CurrentParameterIdentifier { get; }

    uint CurrentParameterChannel { get; }

    string LoggedParameterIdentifier { get; }

    uint LoggedParameterChannel { get; }

    long Timestamp { get; }

    ErrorType ErrorType { get; }

    ErrorStatus ErrorStatus { get; }
}