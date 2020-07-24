namespace physics
{
    partial class FormMainWindow
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
            if (disposing && (components != null))
            {
                components.Dispose();
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
            this.GameCanvas = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.GameCanvas)).BeginInit();
            this.SuspendLayout();
            // 
            // GameCanvas
            // 
            this.GameCanvas.BackColor = System.Drawing.SystemColors.ActiveCaptionText;
            this.GameCanvas.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.GameCanvas.Dock = System.Windows.Forms.DockStyle.Fill;
            this.GameCanvas.Location = new System.Drawing.Point(0, 0);
            this.GameCanvas.Name = "GameCanvas";
            this.GameCanvas.Size = new System.Drawing.Size(822, 719);
            this.GameCanvas.TabIndex = 0;
            this.GameCanvas.TabStop = false;
            this.GameCanvas.Paint += new System.Windows.Forms.PaintEventHandler(this.GameCanvas_DrawGame);
            this.GameCanvas.MouseDown += new System.Windows.Forms.MouseEventHandler(this.GameCanvas_MouseDown);
            this.GameCanvas.MouseMove += new System.Windows.Forms.MouseEventHandler(this.GameCanvas_MouseMove);
            this.GameCanvas.MouseUp += new System.Windows.Forms.MouseEventHandler(this.GameCanvas_MouseUp);
            // 
            // FormMainWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(822, 719);
            this.Controls.Add(this.GameCanvas);
            this.DoubleBuffered = true;
            this.Name = "FormMainWindow";
            this.Text = "Form1";
            this.Load += new System.EventHandler(this.FormMainWindow_Load);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.FormMainWindow_KeyDown);
            ((System.ComponentModel.ISupportInitialize)(this.GameCanvas)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.PictureBox GameCanvas;
    }
}

