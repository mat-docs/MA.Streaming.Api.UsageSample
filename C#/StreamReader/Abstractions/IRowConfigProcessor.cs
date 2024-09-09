// <copyright file="IRowConfigProcessor.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MESL.SqlRace.Domain;

namespace Stream.Api.Stream.Reader.Abstractions;

public interface IRowConfigProcessor
{
    ConfigurationSet CreateConfig(IReadOnlyList<string> parameterIdentifiers, out Dictionary<string, uint> channelsToAdd);
}