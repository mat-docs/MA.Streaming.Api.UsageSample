// <copyright file="ISession.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

namespace Stream.Api.Stream.Reader.Abstractions;

public interface ISession
{
    bool SessionEnded { get; }

    void EndSession();

    void StartSession();
}