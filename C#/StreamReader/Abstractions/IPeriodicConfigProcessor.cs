// <copyright file="IPeriodicConfigProcessor.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MESL.SqlRace.Domain;

namespace Stream.Api.Stream.Reader.Abstractions;

public interface IPeriodicConfigProcessor
{
    public ConfigurationSet CreateConfig(IReadOnlyList<Tuple<string, uint>> parameterIdentifiers, out Dictionary<string, Tuple<uint, uint>> channelsToAdd);
}