// <copyright file="ISqlRaceErrorDto.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

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