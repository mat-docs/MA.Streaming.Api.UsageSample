// <copyright file="ISession.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

namespace Stream.Api.Stream.Reader.Abstractions;

public interface ISession
{
    bool SessionEnded { get; }

    void EndSession();

    void StartSession();
}