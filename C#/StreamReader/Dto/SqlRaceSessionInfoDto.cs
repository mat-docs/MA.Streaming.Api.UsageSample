// <copyright file="SqlRaceSessionInfoDto.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using Stream.Api.Stream.Reader.Abstractions;

namespace Stream.Api.Stream.Reader.Dto
{
    public class SqlRaceSessionInfoDto : ISqlRaceDto, ISqlRaceSessionInfoDto
    {
        public SqlRaceSessionInfoDto(string sessionIdentifier)
        {
            this.SessionIdentifier = sessionIdentifier;
            this.DataType = "Session Info";
        }

        public string DataType { get; }

        public string SessionIdentifier { get; }
    }
}