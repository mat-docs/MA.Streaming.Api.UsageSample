// <copyright file="SessionInfo.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

namespace MA.Streaming.Api.UsageSample.SessionManagement;

public class SessionInfo
{
    public string DataSource { get; }

    public string SessionKey { get; }

    public string Type { get; }

    public uint Version { get; }

    public IReadOnlyList<string> AssociatedKeys { get; }

    public string Identifier { get; }

    public bool IsComplete { get; }

    public SessionInfo(string dataSource, string sessionKey, string type, uint version, IReadOnlyList<string> associatedKeys, string identifier, bool isComplete)
    {
        this.DataSource = dataSource;
        this.SessionKey = sessionKey;
        this.Type = type;
        this.Version = version;
        this.AssociatedKeys = associatedKeys;
        this.Identifier = identifier;
        this.IsComplete = isComplete;
    }
}