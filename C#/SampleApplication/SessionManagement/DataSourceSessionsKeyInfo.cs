// <copyright file="DataSourceSessionsKeyInfo.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

namespace MA.Streaming.Api.UsageSample.SessionManagement;

internal class DataSourceSessionsKeyInfo
{
    public DataSourceSessionsKeyInfo(string dataSource, IReadOnlyList<string> sessionKeys)
    {
        this.DataSource = dataSource;
        this.SessionKeys = sessionKeys;
    }

    public string DataSource { get; }

    public IReadOnlyList<string> SessionKeys { get; }
}