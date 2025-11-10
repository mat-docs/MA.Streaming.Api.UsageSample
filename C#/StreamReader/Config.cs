// <copyright file="Config.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

namespace Stream.Api.Stream.Reader
{
    public class Config
    {
        public string DataSource { get; set; } = "Default";

        public string SQLRaceConnectionString { get; set; } = "server=MCLA-F8ZLSQ3\\LOCAL;Initial Catalog=SQLRACE01_LOCAL;Trusted_Connection=True;";

        public string IPAddress { get; set; } = "127.0.0.1:13579";
    }
}