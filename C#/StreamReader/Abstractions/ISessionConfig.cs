// <copyright file="ISessionConfig.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

using MESL.SqlRace.Domain;

namespace Stream.Api.Stream.Reader.Abstractions;

public interface ISessionConfig
{
    EventDefinition GetEventDefinition(string eventName);

    uint GetParameterChannelId(string parameterName, uint interval);

    uint GetParameterChannelId(string parameterName);

    bool IsEventExistInConfig(string eventName);

    bool IsParameterExistInConfig(string parameterName);

    bool IsParameterExistInConfig(string parameterName, uint interval);

    void SetEventDefinition(string eventName, EventDefinition eventDefinition);

    void SetParameterChannelId(string parameterName, uint interval, uint channelId);

    void SetParameterChannelId(string parameterName, uint channelId);
}