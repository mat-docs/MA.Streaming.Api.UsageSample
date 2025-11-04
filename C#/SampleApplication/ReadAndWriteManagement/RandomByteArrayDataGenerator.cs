// <copyright file="RandomByteArrayDataGenerator.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

namespace MA.Streaming.Api.UsageSample.ReadAndWriteManagement;

internal static class RandomByteArrayDataGenerator
{
    private const int SizeOfRunIdBytes = sizeof(long);
    internal const int SizeOfDateTimeTicks = sizeof(long);
    private const int SizeOfUintOrder = sizeof(uint);
    private static readonly Random Random = new();

    public static byte[] Create(uint size)
    {
        var data = new byte[size - (SizeOfRunIdBytes + SizeOfDateTimeTicks + SizeOfUintOrder)];
        Random.NextBytes(data);
        return data;
    }
}