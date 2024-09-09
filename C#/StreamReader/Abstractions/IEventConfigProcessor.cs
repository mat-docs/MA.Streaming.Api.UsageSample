// <copyright file="IEventConfigProcessor.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MESL.SqlRace.Domain;

namespace Stream.Api.Stream.Reader.Abstractions;

public interface IEventConfigProcessor
{
    ConfigurationSet CreateConfig(IReadOnlyList<string> eventIdentifiers, out Dictionary<string, EventDefinition> eventsToAdd);
}