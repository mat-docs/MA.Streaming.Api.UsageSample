// <copyright file="IPacketHandler.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

namespace Stream.Api.Stream.Reader.Abstractions
{
    public interface IPacketHandler<in T>
    {
        public void Handle(T packet);

        public void Stop();
    }
}