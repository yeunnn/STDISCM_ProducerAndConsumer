using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using AxWMPLib;

namespace STDISCM_ProblemSet3_Consumer
{
    public partial class PreviewForm : Form
    {
        public AxWindowsMediaPlayer PreviewPlayer { get; private set; }

        public PreviewForm()
        {
            InitializeComponent(); // InitializeComponent() managed by the Designer
            BuildUI(); // Manual UI logic goes here

            // Uncomment this to show control scheme in PreviewPlayer
            //this.Load += PreviewForm_Load;
        }

        private void PreviewForm_Load(object sender, EventArgs e)
        {
            // Set uiMode to "none" to hide controls after the control is fully loaded.
            PreviewPlayer.uiMode = "none";
        }

        private void BuildUI()
        {
            this.PreviewPlayer = new AxWindowsMediaPlayer();
            ((System.ComponentModel.ISupportInitialize)(this.PreviewPlayer)).BeginInit();
            this.SuspendLayout();
            // 
            // PreviewPlayer
            // 
            this.PreviewPlayer.Enabled = true;
            this.PreviewPlayer.Location = new System.Drawing.Point(0, 0);
            this.PreviewPlayer.Name = "PreviewPlayer";
            this.PreviewPlayer.Size = new System.Drawing.Size(320, 240); // adjust as needed
            this.PreviewPlayer.TabIndex = 0;
            // 
            // PreviewForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(273, 208);
            this.Controls.Add(this.PreviewPlayer);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "PreviewForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            this.Text = "Preview";
            ((System.ComponentModel.ISupportInitialize)(this.PreviewPlayer)).EndInit();
            this.ResumeLayout(false);
        }
    }
}
