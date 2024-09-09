// <copyright file="BaseMapper.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

namespace Stream.Api.Stream.Reader.SqlRace.Mappers
{
    internal abstract class BaseMapper
    {
        protected BaseMapper(SessionConfig sessionConfig)
        {
            this.SessionConfig = sessionConfig;
        }

        protected SessionConfig SessionConfig { get; }

        public static long ConvertUnixToSqlRaceTime(ulong unixTime)
        {
            return unixTime.ToSqlRaceTime();
        }
    }
}