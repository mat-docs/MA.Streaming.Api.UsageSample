// <copyright file="ISqlRaceRecorder.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

namespace Stream.Api.Stream.Reader.Abstractions;

public interface ISqlRaceRecorder
{
    void StartRecorder();

    void StopRecorder();
}