// <copyright file="LatencyInfo.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

using MA.Streaming.Api.UsageSample.ReadAndWriteManagement;

namespace MA.Streaming.Api.UsageSample;

internal class LatencyInfo
{
    public LatencyInfo(RunInfo runInfo)
    {
        this.RunInfo = runInfo;
    }

    public RunInfo RunInfo { get; }

    public long Id => this.RunInfo.RunId;

    public List<LatencyRecord> ReceiveLatencies { get; } = [];

    public List<LatencyRecord> PublishLatencies { get; } = [];
}