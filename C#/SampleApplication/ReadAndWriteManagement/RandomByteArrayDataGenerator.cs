// <copyright file="RandomByteArrayDataGenerator.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

namespace MA.Streaming.Api.UsageSample.ReadAndWriteManagement;

internal static class RandomByteArrayDataGenerator
{
    private static readonly Random Random = new();
    private const int SizeOfRunIdBytes = sizeof(long);
    internal const int SizeOfDateTimeTicks = sizeof(long);
    private const int SizeOfUintOrder = sizeof(uint);

    public static byte[] Create(uint size)
    {
        var data = new byte[size - (SizeOfRunIdBytes + SizeOfDateTimeTicks + SizeOfUintOrder)];
        Random.NextBytes(data);
        return data;
    }
}