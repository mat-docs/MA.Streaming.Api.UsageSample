namespace MA.Streaming.Api.UsageSample.Utility
{
    partial class FrmLoading
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (this.components != null))
            {
                this.components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.label1 = new Label();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.BackColor = SystemColors.ScrollBar;
            this.label1.Dock = DockStyle.Fill;
            this.label1.Location = new Point(0, 0);
            this.label1.Name = "label1";
            this.label1.Size = new Size(800, 450);
            this.label1.TabIndex = 0;
            this.label1.Text = "please wait ...";
            this.label1.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // FrmLoading
            // 
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(800, 450);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = FormBorderStyle.None;
            this.Name = "FrmLoading";
            this.Opacity = 0.5D;
            this.Text = "FrmLoading";
            this.ResumeLayout(false);
        }

        #endregion

        private Label label1;
    }
}