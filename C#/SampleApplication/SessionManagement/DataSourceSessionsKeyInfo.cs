// <copyright file="DataSourceSessionsKeyInfo.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

namespace MA.Streaming.Api.UsageSample.SessionManagement;

internal class DataSourceSessionsKeyInfo
{
    public string DataSource { get; }

    public IReadOnlyList<string> SessionKeys { get; }

    public DataSourceSessionsKeyInfo(string dataSource, IReadOnlyList<string> sessionKeys)
    {
        this.DataSource = dataSource;
        this.SessionKeys = sessionKeys;
    }
}