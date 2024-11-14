// <copyright file="IPacketHandler.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MA.Streaming.OpenData;

namespace Stream.Api.Stream.Reader.Abstractions
{
    public interface IPacketHandler<in T>
    {
        public void Handle(T packet);

        public void Stop();
    }
}