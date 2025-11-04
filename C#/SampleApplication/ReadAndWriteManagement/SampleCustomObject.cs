// <copyright file="SampleCustomObject.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

namespace MA.Streaming.Api.UsageSample.ReadAndWriteManagement;

public class SampleCustomObject
{
    private const int SizeOfRunIdBytes = sizeof(long);
    private const int SizeOfDateTimeTicks = sizeof(long);
    private const int SizeOfUintOrder = sizeof(uint);

    public SampleCustomObject(long runId, uint order, uint sizeOfSerialisedContent)
        : this(
            runId,
            DateTime.Now,
            order,
            RandomByteArrayDataGenerator.Create(sizeOfSerialisedContent < MinimumObjectSize ? MinimumObjectSize : sizeOfSerialisedContent))
    {
    }

    private SampleCustomObject(long runId, DateTime creationTime, uint order, byte[] content)
    {
        this.RunId = runId;
        this.CreationTime = creationTime;
        this.Order = order;
        this.Content = content;
    }

    public long RunId { get; }

    public DateTime CreationTime { get; }

    public byte[] Content { get; }

    public uint Order { get; }

    public static uint MinimumObjectSize => SizeOfRunIdBytes + SizeOfDateTimeTicks + SizeOfUintOrder + 1;

    public static SampleCustomObject Deserialize(byte[] serializedBytes)
    {
        var runId = BitConverter.ToInt64(serializedBytes, 0);
        var creationTime = DateTime.FromBinary(BitConverter.ToInt64(serializedBytes, SizeOfRunIdBytes));
        var order = BitConverter.ToUInt32(serializedBytes, SizeOfRunIdBytes + SizeOfDateTimeTicks);
        var content = new byte[serializedBytes.Length - (SizeOfRunIdBytes + SizeOfDateTimeTicks + SizeOfUintOrder)];
        Array.Copy(serializedBytes, SizeOfDateTimeTicks + SizeOfUintOrder, content, 0, content.Length);
        return new SampleCustomObject(runId, creationTime, order, content);
    }

    public byte[] Serialize()
    {
        var result = new byte[SizeOfRunIdBytes + SizeOfDateTimeTicks + SizeOfUintOrder + this.Content.Length];
        Array.Copy(BitConverter.GetBytes(this.RunId), 0, result, 0, SizeOfRunIdBytes);
        Array.Copy(BitConverter.GetBytes(this.CreationTime.ToBinary()), 0, result, SizeOfRunIdBytes, SizeOfDateTimeTicks);
        Array.Copy(BitConverter.GetBytes(this.Order), 0, result, SizeOfRunIdBytes + SizeOfDateTimeTicks, SizeOfUintOrder);
        Array.Copy(this.Content, 0, result, SizeOfRunIdBytes + SizeOfDateTimeTicks + SizeOfUintOrder, this.Content.Length);
        return result;
    }
}