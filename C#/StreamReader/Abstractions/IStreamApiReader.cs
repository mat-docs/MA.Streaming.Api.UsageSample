// <copyright file="IStreamApiReader.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

namespace Stream.Api.Stream.Reader.Abstractions
{
    public interface IStreamApiReader
    {
        public void Start();

        public void Stop();
    }
}