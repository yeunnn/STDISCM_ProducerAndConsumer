using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace STDISCM_ProblemSet3_Consumer
{
    public partial class ConfigForm : Form
    {
        public int ConsumerThreadsCount { get; private set; }
        public int QueueCapacity { get; private set; }
        public int ListeningPort { get; private set; }

        public ConfigForm()
        {
            InitializeComponent(); // InitializeComponent() managed by the Designer
            BuildUI(); // Manual UI logic goes here
        }

        private void BuildUI()
        {
            this.Text = "Consumer Configuration";
            this.Width = 300;
            this.Height = 200;

            Label labelThreads = new Label() { Left = 10, Top = 20, Text = "Consumer Threads:" };
            TextBox textBoxThreads = new TextBox() { Left = 150, Top = 20, Width = 100, Text = "2" };

            Label labelQueue = new Label() { Left = 10, Top = 60, Text = "Queue Capacity:" };
            TextBox textBoxQueue = new TextBox() { Left = 150, Top = 60, Width = 100, Text = "10" };

            Label labelPort = new Label() { Left = 10, Top = 100, Text = "Listening Port:" };
            TextBox textBoxPort = new TextBox() { Left = 150, Top = 100, Width = 100, Text = "9000" };

            Button btnOK = new Button() { Text = "OK", Left = 50, Width = 80, Top = 140, DialogResult = DialogResult.OK };
            Button btnCancel = new Button() { Text = "Cancel", Left = 150, Width = 80, Top = 140, DialogResult = DialogResult.Cancel };

            btnOK.Click += (sender, e) =>
            {
                if (int.TryParse(textBoxThreads.Text, out int threads) &&
                    int.TryParse(textBoxQueue.Text, out int queueCap) &&
                    int.TryParse(textBoxPort.Text, out int port))
                {
                    ConsumerThreadsCount = threads;
                    QueueCapacity = queueCap;
                    ListeningPort = port;
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Please enter valid numbers.");
                }
            };

            this.Controls.Add(labelThreads);
            this.Controls.Add(textBoxThreads);
            this.Controls.Add(labelQueue);
            this.Controls.Add(textBoxQueue);
            this.Controls.Add(labelPort);
            this.Controls.Add(textBoxPort);
            this.Controls.Add(btnOK);
            this.Controls.Add(btnCancel);
        }
    }
}
