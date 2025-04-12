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

        /*
        * Builds the user interface for the consumer application
        */
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

        /*
        * Handles mouse movement over the ListView to show video previews
        *
        * @param sender - The object that raised the event
        * @param e - The mouse event arguments
        */
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

        /*
        * Handles mouse click events on the ListView to start full playback of a selected video
        *
        * @param sender - The object that raised the event
        * @param e - The mouse event arguments
        */
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

        /*
        * Stops the preview when the timer ticks (after 10 seconds)
        *
        * @param sender - The object that raised the event
        * @param e - The event arguments
        */
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


        /*
        * Starts the network listener to accept incoming video uploads
        */
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

        /*
        * Handles an individual client upload by receiving the video file,
        * compressing it if it exceeds 10 MB, performing duplicate detection,
        * and enqueuing it for processing. The final response message includes 
        * information about compression status.
        *
        * @param client - The TcpClient representing the connection to the producer
        */
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
                    string originalFileName = Encoding.UTF8.GetString(nameBuffer);

                    // Read file size (8 bytes)
                    byte[] longBuffer = ReadExact(ns, 8);
                    if (longBuffer == null) return;
                    long fileSize = IPAddress.NetworkToHostOrder(BitConverter.ToInt64(longBuffer, 0));

                    // Now, read the file data.
                    byte[] fileData = ReadExact(ns, (int)fileSize);
                    if (fileData == null) return;

                    // Compression: If the file is larger than 10 MB, compress it using FFmpeg.
                    const long threshold = 10 * 1024 * 1024;
                    string compressionMsg = "";
                    if (fileSize > threshold)
                    {
                        // Generate a unique temporary file name to avoid collisions.
                        string safeFileName = Path.GetFileName(originalFileName);
                        string uniqueTempName = $"{Path.GetFileNameWithoutExtension(safeFileName)}_{Guid.NewGuid()}{Path.GetExtension(safeFileName)}";
                        string tempInputFile = Path.Combine(Path.GetTempPath(), uniqueTempName);

                        File.WriteAllBytes(tempInputFile, fileData);

                        // Call CompressVideo with the new out parameter for error/status info.
                        if (CompressVideo(tempInputFile, out byte[] newFileData, out long newSize, out compressionMsg))
                        {
                            fileData = newFileData;
                            fileSize = newSize;
                        }
                        // Delete the temporary input file.
                        File.Delete(tempInputFile);
                    }

                    // Duplicate detection: Check if a file with the same name already exists in "UploadedVideos"
                    string finalFileName = originalFileName;
                    string filePath = Path.Combine("UploadedVideos", finalFileName);
                    string duplicateMsg = "";
                    if (File.Exists(filePath))
                    {
                        // Duplicate found; generate a unique name.
                        string newFileName = GetUniqueFileName(originalFileName);
                        duplicateMsg = $" Duplicate detected, renamed: {newFileName}";
                        finalFileName = newFileName;
                    }

                    // Create the VideoUpload object using the final file name.
                    VideoUpload vu = new VideoUpload { FileName = finalFileName, Data = fileData };

                    // Attempt to enqueue the video.
                    bool enqueued = videoQueue.TryEnqueue(vu);
                    string responseMsg = "";
                    if (!enqueued)
                    {
                        responseMsg = $"QUEUE_FULL: Dropping file: {originalFileName}";
                    }
                    else
                    {
                        responseMsg = !string.IsNullOrEmpty(duplicateMsg)
                            ? $"DUPLICATE: {originalFileName}{duplicateMsg}{compressionMsg}"
                            : $"OK: File accepted: {originalFileName}{compressionMsg}";
                    }

                    // Send final response to the producer.
                    byte[] response = Encoding.UTF8.GetBytes(responseMsg);
                    ns.Write(response, 0, response.Length);
                    ns.Flush();
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

        /*
        * Compresses a video file using ffmpeg to reduce its size if it exceeds a certain threshold
        *
        * @param inputPath - Path to the video file to be compressed
        * @param compressedData - The compressed video data
        * @param newSize - The size of the compressed video data
        * @param compressionMsg - A message detailing the compression outcome or errors encountered
        *
        * @return true if compression is successful, false if compression fails
        */
        static bool CompressVideo(string inputPath, out byte[] compressedData, out long newSize, out string compressionMsg)
        {
            string ffmpegPath = Path.Combine(Directory.GetCurrentDirectory(), "FFMPEG", "bin", "ffmpeg.exe");

            if (!File.Exists(ffmpegPath))
            {
                compressedData = null;
                newSize = 0;
                compressionMsg = " | COMPRESSION_FAILED: ffmpeg.exe not found.";
                return false;
            }

            // Unique output file name to avoid overwrite prompt
            string outputFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.mp4");

            // Always overwrite (-y), suppress unnecessary prompts
            string arguments = $"-y -i \"{inputPath}\" -vcodec libx264 -crf 28 -preset fast -acodec copy \"{outputFile}\"";

            var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = ffmpegPath;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;

            StringBuilder errorOutput = new StringBuilder();

            try
            {
                process.Start();

                // Start reading both output and error to prevent blocking
                Task<string> stderrTask = process.StandardError.ReadToEndAsync();
                Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();

                if (!process.WaitForExit(30000)) // 30 seconds timeout
                {
                    try { process.Kill(); } catch { }
                    compressedData = null;
                    newSize = 0;
                    compressionMsg = " | COMPRESSION_FAILED: Timeout after 30s.";
                    return false;
                }

                string stderr = stderrTask.Result;
                string stdout = stdoutTask.Result;

                if (File.Exists(outputFile))
                {
                    compressedData = File.ReadAllBytes(outputFile);
                    newSize = compressedData.Length;
                    File.Delete(outputFile);
                    compressionMsg = $" | COMPRESSED: New size: {newSize / (1024.0 * 1024.0):0.00} MB";
                    return true;
                }
                else
                {
                    compressedData = null;
                    newSize = 0;
                    compressionMsg = $" | COMPRESSION_FAILED: FFmpeg didn't output file.\nStderr: {stderr}";
                    return false;
                }
            }
            catch (Exception ex)
            {
                compressedData = null;
                newSize = 0;
                compressionMsg = $" | COMPRESSION_FAILED: Exception - {ex.Message}";
                return false;
            }
        }

        /*
        * Generates a unique file name if a file with the same name already exists in the "UploadedVideos" folder
        *
        * @param originalFileName - The original name of the video file
        *
        * @return - A unique file name
        */
        private string GetUniqueFileName(string originalFileName)
        {
            string folder = "UploadedVideos";
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(originalFileName);
            string extension = Path.GetExtension(originalFileName);
            string newFileName = originalFileName;
            int copyNumber = 1;

            // Loop until you find a file name that does not exist
            while (File.Exists(Path.Combine(folder, newFileName)))
            {
                newFileName = $"{fileNameWithoutExt}_copy{copyNumber}{extension}";
                copyNumber++;
            }
            return newFileName;
        }

        /*
        * Reads an exact number of bytes from the network stream
        *
        * @param ns - The network stream to read from
        * @param size - The number of bytes to read
        *
        * @return - The byte array containing the read data
        */
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

        /*
        * Processes the video queue by saving videos to disk and updating the UI
        * Continuously dequeues video data from the shared queue,
        * writes it to the "UploadedVideos" directory, and updates the ListView
        * on the UI thread to display newly saved videos.
        */
        private void ProcessQueue()
        {
            while (true)
            {
                VideoUpload vu = videoQueue.Dequeue();
                // Save the file to disk
                string filePath = Path.Combine("UploadedVideos", vu.FileName);
                File.WriteAllBytes(filePath, vu.Data);
                Console.WriteLine("Saved file: " + vu.FileName);

                // Introduce an artificial delay because otherwise it would process too fast
                Thread.Sleep(5000);

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

        /*
        * Loads existing videos from the "UploadedVideos" folder into the ListView
        * Scans the "UploadedVideos" directory for files and adds them
        * to the ListView so users can see previously uploaded videos on startup.
        */
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
