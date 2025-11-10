// <copyright file="FrmLoading.cs" company="Motion Applied Ltd.">
// Copyright (c) Motion Applied Ltd.</copyright>

using System.Timers;

using Timer = System.Timers.Timer;

namespace MA.Streaming.Api.UsageSample.Utility;

public partial class FrmLoading : Form
{
    private readonly Timer timer = new();

    public FrmLoading()
    {
        this.InitializeComponent();
        this.timer.Elapsed += this.Timer_Elapsed;
    }

    public void ShowOnForm(Form parentForm, TimeSpan? showTime = null)
    {
        this.StartPosition = FormStartPosition.Manual;
        this.DesktopLocation = parentForm.DesktopLocation;
        this.Size = parentForm.Size;
        if (showTime != null)
        {
            this.timer.Interval = showTime.Value.TotalMilliseconds;
            this.timer.Enabled = true;
        }

        this.ShowDialog();
    }

    private void Timer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        this.Invoke((MethodInvoker)this.Close);
        this.timer.Enabled = false;
    }
}