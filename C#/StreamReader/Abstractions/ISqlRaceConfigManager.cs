// <copyright file="ISqlRaceConfigManager.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MESL.SqlRace.Domain;

namespace Stream.Api.Stream.Reader.Abstractions;

public interface ISqlRaceConfigManager
{
    void AddBasicEventConfiguration(IReadOnlyList<string> eventIdentifiers);

    void AddBasicPeriodicParameterConfiguration(
        IReadOnlyList<Tuple<string, uint>> parameterIdentifiers);

    void AddBasicRowParameterConfiguration(
        IReadOnlyList<string> parameterIdentifiers);

    bool IsEventExistInConfig(string eventName);

    bool IsParameterExistInConfig(string parameterName);

    bool IsParameterExistInConfig(string parameterName, uint interval);

    uint GetParameterChannelId(string parameterName, uint interval);

    uint GetParameterChannelId(string parameterName);

    EventDefinition GetEventDefinition(string eventName);

    public void AddEventToConfigs(string eventName);

    public void AddParametersToConfigs(Tuple<string, uint> parameterList);

    public void AddParametersToConfigs(List<string> parameterList);
}