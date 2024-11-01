// <copyright file="IConfigProcessor.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

namespace MA.DataPlatforms.DataRecorder.SqlRaceWriter.Abstractions
{
    internal interface IConfigProcessor<in T>
    {
        public event EventHandler ProcessCompleted;

        public void AddToConfig(T item);
    }
}