// <copyright file="WindowsFormLogger.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

using MA.Common.Abstractions;

namespace MA.Streaming.Api.UsageSample;

public class WindowsFormLogger : ILogger
{
    public void Debug(string message)
    {
        MessageBox.Show(message, "Debug", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public void Error(string message)
    {
        MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    public void Info(string message)
    {
        MessageBox.Show(message, "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public void Trace(string message)
    {
        MessageBox.Show(message, "Trace", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public void Warning(string message)
    {
        MessageBox.Show(message, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}