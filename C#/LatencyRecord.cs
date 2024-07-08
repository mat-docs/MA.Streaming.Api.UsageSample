// <copyright file="LatencyRecord.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

namespace MA.Streaming.Api.UsageSample;

internal class LatencyRecord
{
    public uint NumberOfMessages { get; }

    public double MaxLatency { get; }

    public LatencyRecord(uint numberOfMessages, double maxLatency)
    {
        this.NumberOfMessages = numberOfMessages;
        this.MaxLatency = maxLatency;
    }
}