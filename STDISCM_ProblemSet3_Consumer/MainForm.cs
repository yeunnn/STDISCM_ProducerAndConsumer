using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using WMPLib;
using AxWMPLib; // Be sure to add COM Reference for Windows Media Player

namespace STDISCM_ProblemSet3_Consumer
{
    // Class representing an uploaded video
    public class VideoUpload
    {
        public string FileName { get; set; }
        public byte[] Data { get; set; }
    }

    // Bounded queue that drops items if full (leaky bucket)
    public class BoundedQueue<T>
    {
        private Queue<T> queue = new Queue<T>();
        private int capacity;
        internal object lockObj = new object();
        private AutoResetEvent itemEnqueued = new AutoResetEvent(false);

        public BoundedQueue(int capacity)
        {
            this.capacity = capacity;
        }

        // Expose Count
        public int Count
        {
            get { lock (lockObj) { return queue.Count; } }
        }

        // Expose a helper property to check if the queue is full
        public bool IsFull
        {
            get { lock (lockObj) { return queue.Count >= capacity; } }
        }

        // Try to enqueue an item; return false if the queue is full.
        public bool TryEnqueue(T item)
        {
            lock (lockObj)
            {
                if (queue.Count >= capacity)
                {
                    return false; // Drop the item
                }
                queue.Enqueue(item);
                itemEnqueued.Set();
                return true;
            }
        }

        // Blocking dequeue (waits until an item is available)
        public T Dequeue()
        {
            while (true)
            {
                lock (lockObj)
                {
                    if (queue.Count > 0)
                    {
                        return queue.Dequeue();
                    }
                }
                itemEnqueued.WaitOne(100);
            }
        }
    }

    public partial class MainForm : Form
    {
        private ListView listViewVideos;
        private AxWindowsMediaPlayer mediaPlayer;
        // Use the Windows Forms Timer to control preview duration.
        private System.Windows.Forms.Timer previewTimer;

        // Field to track if full playback is active.
        private bool isFullPlayback = false;

        // New field to track which ListViewItem is currently under the mouse.
        private ListViewItem lastItemUnderMouse = null;

        // Other fields (for networking, queueing, etc.)
        private BoundedQueue<VideoUpload> videoQueue;
        private int consumerThreadsCount;
        private int listeningPort;

        // PreviewForm
        private PreviewForm previewForm = null;

        public MainForm(int consumerThreadsCount, int queueCapacity, int listeningPort)
        {
            this.consumerThreadsCount = consumerThreadsCount;
            this.listeningPort = listeningPort;
            videoQueue = new BoundedQueue<VideoUpload>(queueCapacity);

            InitializeComponent();
            BuildUI();

            // Ensure the folder for uploaded videos exists.
            if (!Directory.Exists("UploadedVideos"))
            {
                Directory.CreateDirectory("UploadedVideos");
            }

            // Ensure the UploadedVideos folder exists and load any existing files.
            LoadExistingVideos();

            // Set up the preview timer (10 seconds).
            previewTimer = new System.Windows.Forms.Timer();
            previewTimer.Interval = 10000; // 10 seconds
            previewTimer.Tick += previewTimer_Tick;

            // Subscribe to the MouseMove event on the ListView.
            listViewVideos.MouseMove += listViewVideos_MouseMove;
            listViewVideos.MouseClick += listViewVideos_MouseClick;

            // Start the network listener thread.
            Thread listenerThread = new Thread(new ThreadStart(StartListener));
            listenerThread.IsBackground = true;
            listenerThread.Start();

            // Start the worker threads to process the upload queue.
            for (int i = 0; i < consumerThreadsCount; i++)
            {
                Thread worker = new Thread(new ThreadStart(ProcessQueue));
                worker.IsBackground = true;
                worker.Start();
            }
        }

        private void BuildUI()
        {
            this.listViewVideos = new ListView();
            this.mediaPlayer = new AxWindowsMediaPlayer();
            ((System.ComponentModel.ISupportInitialize)(this.mediaPlayer)).BeginInit();
            this.SuspendLayout();
            // 
            // listViewVideos
            // 
            this.listViewVideos.Location = new System.Drawing.Point(12, 12);
            this.listViewVideos.Size = new System.Drawing.Size(300, 400);
            this.listViewVideos.View = View.List;
            this.listViewVideos.FullRowSelect = true;
            // (Other event handlers such as Click for full playback can be added here)
            // 
            // mediaPlayer
            // 
            this.mediaPlayer.Enabled = true;
            this.mediaPlayer.Location = new System.Drawing.Point(320, 12);
            this.mediaPlayer.Size = new System.Drawing.Size(480, 400);
            this.mediaPlayer.Name = "mediaPlayer";
            // 
            // Form1
            // 
            this.ClientSize = new System.Drawing.Size(820, 430);
            this.Controls.Add(this.listViewVideos);
            this.Controls.Add(this.mediaPlayer);
            this.Name = "Form1";
            this.Text = "Media Upload Consumer";
            ((System.ComponentModel.ISupportInitialize)(this.mediaPlayer)).EndInit();
            this.ResumeLayout(false);
        }

        // MouseMove event handler to perform a hit test.
        private void listViewVideos_MouseMove(object sender, MouseEventArgs e)
        {
            // If a full playback is in progress (if you want to prevent preview during full playback), you can check a flag here. (Setup is in always allow preview)
            var hitTestInfo = listViewVideos.HitTest(e.X, e.Y);
            ListViewItem item = hitTestInfo.Item;

            if (item != null && item != lastItemUnderMouse)
            {
                lastItemUnderMouse = item;

                // Stop any current preview.
                previewTimer.Stop();
                if (previewForm != null)
                {
                    previewForm.Close();
                    previewForm = null;
                }

                string fileName = item.Text;
                string filePath = System.IO.Path.Combine("UploadedVideos", fileName);
                if (System.IO.File.Exists(filePath))
                {
                    // Create and position the preview form.
                    previewForm = new PreviewForm();
                    // Optionally, position near the mouse pointer:
                    System.Drawing.Point location = listViewVideos.PointToScreen(new System.Drawing.Point(e.X, e.Y));
                    previewForm.Location = location;

                    previewForm.Show();

                    // Start preview in the preview form's media player.
                    previewForm.PreviewPlayer.URL = filePath;
                    previewForm.PreviewPlayer.Ctlcontrols.play();

                    // Start the preview timer (to close it after 10 seconds).
                    previewTimer.Start();
                }
            }
            else if (item == null)
            {
                // If mouse is not over any item, reset the last item and close any preview.
                lastItemUnderMouse = null;
                previewTimer.Stop();
                if (previewForm != null)
                {
                    previewForm.Close();
                    previewForm = null;
                }
            }
        }

        private void listViewVideos_MouseClick(object sender, MouseEventArgs e)
        {
            var hitTestInfo = listViewVideos.HitTest(e.X, e.Y);
            if (hitTestInfo.Item != null)
            {
                // Close the preview popup if it's open.
                previewTimer.Stop();
                if (previewForm != null)
                {
                    previewForm.Close();
                    previewForm = null;
                }

                // Proceed to full playback in the main media player.
                string fileName = hitTestInfo.Item.Text;
                string filePath = System.IO.Path.Combine("UploadedVideos", fileName);
                if (System.IO.File.Exists(filePath))
                {
                    mediaPlayer.URL = filePath;
                    mediaPlayer.Ctlcontrols.play();
                }
            }
        }

        // Stop the preview when the timer ticks (after 10 seconds).
        private void previewTimer_Tick(object sender, EventArgs e)
        {
            previewTimer.Stop();
            if (previewForm != null)
            {
                previewForm.Close();
                previewForm = null;
            }
            lastItemUnderMouse = null;
        }


        // Network listener that accepts incoming video uploads.
        private void StartListener()
        {
            TcpListener listener = new TcpListener(IPAddress.Any, listeningPort);
            listener.Start();
            Console.WriteLine("Consumer listening on port " + listeningPort);
            while (true)
            {
                try
                {
                    TcpClient client = listener.AcceptTcpClient();
                    Thread clientThread = new Thread(() => HandleClient(client));
                    clientThread.IsBackground = true;
                    clientThread.Start();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error accepting client: " + ex.Message);
                }
            }
        }

        // Handle an individual client upload.
        private void HandleClient(TcpClient client)
        {
            try
            {
                using (var ns = client.GetStream())
                {
                    // Read file name length (4 bytes)
                    byte[] intBuffer = ReadExact(ns, 4);
                    if (intBuffer == null) return;
                    int fileNameLength = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(intBuffer, 0));

                    // Read the file name
                    byte[] nameBuffer = ReadExact(ns, fileNameLength);
                    if (nameBuffer == null) return;
                    string fileName = Encoding.UTF8.GetString(nameBuffer);

                    // Read file size (8 bytes)
                    byte[] longBuffer = ReadExact(ns, 8);
                    if (longBuffer == null) return;
                    long fileSize = IPAddress.NetworkToHostOrder(BitConverter.ToInt64(longBuffer, 0));

                    // --- Begin new bonus logic ---
                    // Check if the queue is full:
                    if (videoQueue.IsFull)
                    {
                        // Notify the producer that the queue is full with the filename info.
                        string responseMsg = "QUEUE_FULL:" + fileName;
                        byte[] response = Encoding.UTF8.GetBytes(responseMsg);
                        ns.Write(response, 0, response.Length);
                        Console.WriteLine($"Queue is full. Dropping file: {fileName} and notifying producer.");
                        return; // Do not proceed further.
                    }
                    else
                    {
                        // Signal OK so that the producer knows to send the file data.
                        byte[] response = Encoding.UTF8.GetBytes("OK");
                        ns.Write(response, 0, response.Length);
                    }
                    // --- End bonus logic ---

                    // Now, read the file data
                    byte[] fileData = ReadExact(ns, (int)fileSize);
                    if (fileData == null) return;

                    VideoUpload vu = new VideoUpload { FileName = fileName, Data = fileData };

                    bool enqueued = videoQueue.TryEnqueue(vu);
                    if (!enqueued)
                    {
                        Console.WriteLine("Queue full. Dropping file: " + fileName);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error handling client: " + ex.Message);
            }
            finally
            {
                client.Close();
            }
        }

        // Helper method to read an exact number of bytes from the stream.
        private byte[] ReadExact(NetworkStream ns, int size)
        {
            byte[] buffer = new byte[size];
            int totalRead = 0;
            while (totalRead < size)
            {
                int bytesRead = ns.Read(buffer, totalRead, size - totalRead);
                if (bytesRead == 0)
                {
                    return null;
                }
                totalRead += bytesRead;
            }
            return buffer;
        }

        // Worker thread that processes the video queue.
        private void ProcessQueue()
        {
            while (true)
            {
                VideoUpload vu = videoQueue.Dequeue();
                // Save the file to disk
                string filePath = Path.Combine("UploadedVideos", vu.FileName);
                File.WriteAllBytes(filePath, vu.Data);
                Console.WriteLine("Saved file: " + vu.FileName);

                // Update the ListView on the UI thread.
                this.Invoke((MethodInvoker)delegate {
                    if (!listViewVideos.Items.ContainsKey(vu.FileName))
                    {
                        ListViewItem item = new ListViewItem(vu.FileName);
                        item.Name = vu.FileName;
                        listViewVideos.Items.Add(item);
                    }
                });
            }
        }

        private void LoadExistingVideos()
        {
            // Ensure the folder exists
            if (!Directory.Exists("UploadedVideos"))
            {
                Directory.CreateDirectory("UploadedVideos");
            }

            // Get all files from the folder
            string[] files = Directory.GetFiles("UploadedVideos");
            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);
                // Add to ListView if not already added
                if (!listViewVideos.Items.ContainsKey(fileName))
                {
                    ListViewItem item = new ListViewItem(fileName)
                    {
                        Name = fileName
                    };
                    listViewVideos.Items.Add(item);
                }
            }
        }

    }
}
