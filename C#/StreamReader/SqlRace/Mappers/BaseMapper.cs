// <copyright file="BaseMapper.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

namespace Stream.Api.Stream.Reader.SqlRace.Mappers
{
    public class BaseMapper
    {
        private const long NumberOfNanosecondsInDay = 86400000000000;

        protected SessionConfig SessionConfig { get; set; }

        public static long ConvertUnixToSqlRaceTime(ulong unixTime)
        {
            return (long)unixTime % NumberOfNanosecondsInDay;
        }
    }
}