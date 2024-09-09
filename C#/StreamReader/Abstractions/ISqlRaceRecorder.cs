// <copyright file="ISqlRaceRecorder.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

namespace Stream.Api.Stream.Reader.Abstractions;

public interface ISqlRaceRecorder
{
    void StartRecorder();

    void StopRecorder();
}