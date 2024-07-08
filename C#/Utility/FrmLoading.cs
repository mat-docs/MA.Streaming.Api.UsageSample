// <copyright file="FrmLoading.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using System.Timers;

using Timer = System.Timers.Timer;

namespace MA.Streaming.Api.UsageSample.Utility
{
    public partial class FrmLoading : Form
    {
        private readonly Timer timer = new();

        public FrmLoading()
        {
            this.InitializeComponent();
            this.timer.Elapsed += this.Timer_Elapsed;
        }

        private void Timer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            this.Invoke((MethodInvoker)this.Close);
            this.timer.Enabled = false;
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
    }
}