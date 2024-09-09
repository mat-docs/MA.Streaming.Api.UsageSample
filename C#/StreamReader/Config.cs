// <copyright file="Config.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

namespace Stream.Api.Stream.Reader
{
    public class Config
    {
        public string DataSource { get; } = "Default";

        public string SQLRaceConnectionString { get; } = "server=MCLA-F8ZLSQ3\\LOCAL;Initial Catalog=SQLRACE01_LOCAL;Trusted_Connection=True;";

        public string IPAddress { get; } = "127.0.0.1:13579";
    }
}