// <copyright file="LatencyInfo.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MA.Streaming.Api.UsageSample.ReadAndWriteManagement;

namespace MA.Streaming.Api.UsageSample;

internal class LatencyInfo
{
    public RunInfo RunInfo { get; }

    public LatencyInfo(RunInfo runInfo)
    {
        this.RunInfo = runInfo;
    }

    public long Id => RunInfo.RunId;
    public List<LatencyRecord> ReceiveLatencies { get; } = [];

    public List<LatencyRecord> PublishLatencies { get; } = [];
}