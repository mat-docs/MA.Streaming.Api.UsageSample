// <copyright file="SqlRaceSessionInfoDto.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using Stream.Api.Stream.Reader.Abstractions;

namespace Stream.Api.Stream.Reader.Dto
{
    public class SqlRaceSessionInfoDto : ISqlRaceDto, ISqlRaceSessionInfoDto
    {
        public string DataType { get; } = "Session Info";

        public string SessionIdentifier { get; set; }
    }
}