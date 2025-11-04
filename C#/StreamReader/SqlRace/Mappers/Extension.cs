// <copyright file="Extension.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

namespace Stream.Api.Stream.Reader.SqlRace.Mappers;

internal static class Extension
{
    private const long NumberOfNanosecondsInDay = 86400000000000;

    public static long ToSqlRaceTime(this ulong unixTime)
    {
        return (long)unixTime % NumberOfNanosecondsInDay;
    }
}