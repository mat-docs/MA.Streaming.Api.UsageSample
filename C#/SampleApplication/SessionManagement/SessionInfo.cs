// <copyright file="SessionInfo.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

namespace MA.Streaming.Api.UsageSample.SessionManagement;

public class SessionInfo
{
    public SessionInfo(
        string dataSource,
        string sessionKey,
        string type,
        uint version,
        IReadOnlyList<string> associatedKeys,
        string identifier,
        bool isComplete,
        long mainOffset,
        long essentialOffset,
        IReadOnlyList<string> streams,
        IDictionary<string, long> topicPartitionsOffset)

    {
        this.DataSource = dataSource;
        this.SessionKey = sessionKey;
        this.Type = type;
        this.Version = version;
        this.AssociatedKeys = associatedKeys;
        this.Identifier = identifier;
        this.IsComplete = isComplete;
        this.MainOffset = mainOffset;
        this.EssentialOffset = essentialOffset;
        this.Streams = streams;
        this.TopicPartitionsOffset = topicPartitionsOffset;
    }

    public string DataSource { get; }

    public string SessionKey { get; }

    public string Type { get; }

    public uint Version { get; }

    public IReadOnlyList<string> AssociatedKeys { get; }

    public string Identifier { get; }

    public bool IsComplete { get; }

    public long MainOffset { get; }

    public long EssentialOffset { get; }

    public IReadOnlyList<string> Streams { get; }

    public IDictionary<string, long> TopicPartitionsOffset { get; }
}