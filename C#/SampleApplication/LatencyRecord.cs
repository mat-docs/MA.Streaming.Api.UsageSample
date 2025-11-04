// <copyright file="LatencyRecord.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

namespace MA.Streaming.Api.UsageSample;

internal class LatencyRecord
{
    public LatencyRecord(uint numberOfMessages, double maxLatency)
    {
        this.NumberOfMessages = numberOfMessages;
        this.MaxLatency = maxLatency;
    }

    public uint NumberOfMessages { get; }

    public double MaxLatency { get; }
}