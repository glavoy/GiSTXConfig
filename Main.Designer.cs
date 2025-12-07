namespace generatexml
{
    partial class Main
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Main));
            this.ButtonGenerate = new System.Windows.Forms.Button();
            this.labelVersion = new System.Windows.Forms.Label();
            this.pictureBoxGisTX = new System.Windows.Forms.PictureBox();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.labelStatus = new System.Windows.Forms.Label();
            this.labelProgress = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxGisTX)).BeginInit();
            this.SuspendLayout();
            // 
            // ButtonGenerate
            // 
            this.ButtonGenerate.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ButtonGenerate.Location = new System.Drawing.Point(371, 166);
            this.ButtonGenerate.Margin = new System.Windows.Forms.Padding(4);
            this.ButtonGenerate.Name = "ButtonGenerate";
            this.ButtonGenerate.Size = new System.Drawing.Size(297, 98);
            this.ButtonGenerate.TabIndex = 0;
            this.ButtonGenerate.Text = "Generate Survey Package";
            this.ButtonGenerate.UseVisualStyleBackColor = true;
            this.ButtonGenerate.Click += new System.EventHandler(this.ButtonXML_Click);
            // 
            // labelVersion
            // 
            this.labelVersion.AutoSize = true;
            this.labelVersion.Font = new System.Drawing.Font("Microsoft Sans Serif", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelVersion.Location = new System.Drawing.Point(13, 500);
            this.labelVersion.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelVersion.Name = "labelVersion";
            this.labelVersion.Size = new System.Drawing.Size(64, 17);
            this.labelVersion.TabIndex = 1;
            this.labelVersion.Text = "Version: ";
            // 
            // pictureBoxGisTX
            // 
            this.pictureBoxGisTX.Image = global::generatexml.Properties.Resources.gistx;
            this.pictureBoxGisTX.InitialImage = ((System.Drawing.Image)(resources.GetObject("pictureBoxGisTX.InitialImage")));
            this.pictureBoxGisTX.Location = new System.Drawing.Point(37, 27);
            this.pictureBoxGisTX.Name = "pictureBoxGisTX";
            this.pictureBoxGisTX.Size = new System.Drawing.Size(151, 125);
            this.pictureBoxGisTX.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBoxGisTX.TabIndex = 5;
            this.pictureBoxGisTX.TabStop = false;
            //
            // progressBar (centered: (1067-631)/2 = 218)
            //
            this.progressBar.Location = new System.Drawing.Point(218, 300);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(631, 28);
            this.progressBar.TabIndex = 6;
            this.progressBar.Visible = false;
            //
            // labelProgress (to the right of progress bar)
            //
            this.labelProgress.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelProgress.ForeColor = System.Drawing.Color.Black;
            this.labelProgress.Location = new System.Drawing.Point(860, 300);
            this.labelProgress.Name = "labelProgress";
            this.labelProgress.Size = new System.Drawing.Size(120, 28);
            this.labelProgress.TabIndex = 8;
            this.labelProgress.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.labelProgress.Visible = false;
            //
            // labelStatus (aligned with progress bar left edge)
            //
            this.labelStatus.AutoSize = true;
            this.labelStatus.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelStatus.Location = new System.Drawing.Point(218, 340);
            this.labelStatus.Name = "labelStatus";
            this.labelStatus.Size = new System.Drawing.Size(0, 18);
            this.labelStatus.TabIndex = 7;
            //
            // Main
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1067, 554);
            this.Controls.Add(this.labelProgress);
            this.Controls.Add(this.labelStatus);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.pictureBoxGisTX);
            this.Controls.Add(this.labelVersion);
            this.Controls.Add(this.ButtonGenerate);
            this.Margin = new System.Windows.Forms.Padding(4);
            this.Name = "Main";
            this.Text = "GiSTConfigX";
            this.Load += new System.EventHandler(this.Main_Load);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxGisTX)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button ButtonGenerate;
        private System.Windows.Forms.Label labelVersion;
        private System.Windows.Forms.PictureBox pictureBoxGisTX;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.Label labelStatus;
        private System.Windows.Forms.Label labelProgress;
    }
}

