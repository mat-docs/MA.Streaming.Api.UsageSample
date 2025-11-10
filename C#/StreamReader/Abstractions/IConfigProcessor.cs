// <copyright file="IConfigProcessor.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

namespace MA.DataPlatforms.DataRecorder.SqlRaceWriter.Abstractions
{
    internal interface IConfigProcessor<in T>
    {
        public event EventHandler ProcessCompleted;

        public void AddToConfig(T item);
    }
}