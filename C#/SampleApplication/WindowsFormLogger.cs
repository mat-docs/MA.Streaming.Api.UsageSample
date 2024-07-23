// <copyright file="WindowsFormLogger.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using MA.Common.Abstractions;

namespace MA.Streaming.Api.UsageSample;

public class WindowsFormLogger : ILogger
{
    public void Debug(string message)
    {
        MessageBox.Show(message, "Debug", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public void Info(string message)
    {
        MessageBox.Show(message, "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public void Warning(string message)
    {
        MessageBox.Show(message, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    public void Error(string message)
    {
        MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    public void Trace(string message)
    {
        MessageBox.Show(message, "Trace", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}