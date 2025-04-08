// Argamosa, Daniel Cedric (S14)
// Donato, Adriel Joseph (S12)

// See https://aka.ms/new-console-template for more information
// Console.WriteLine("Hello, World!");

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace STDISCM_ProblemSet3_Producer
{
    internal class Producer
    {
        static string consumerIP;
        static int consumerPort;
        static string[] directories;

        static void Main(string[] args)
        {
            // IP address validation
            while (true)
            {
                Console.Write("Enter Consumer IP: ");
                consumerIP = Console.ReadLine();
                if (!System.Net.IPAddress.TryParse(consumerIP, out _))
                {
                    Console.WriteLine("Invalid IP address. Try again.");
                }
                else break;
            }

            // Port validation
            while (true)
            {
                Console.Write("Enter Consumer Port: ");
                if (!int.TryParse(Console.ReadLine(), out consumerPort) || consumerPort < 1 || consumerPort > 65535)
                {
                    Console.WriteLine("Invalid port number. Enter a number between 1 and 65535.");
                }
                else break;
            }

            // Producer thread count validation
            int numThreads;
            while (true)
            {
                Console.Write("Enter number of producer threads (directories): ");
                if (!int.TryParse(Console.ReadLine(), out numThreads) || numThreads < 1)
                {
                    Console.WriteLine("Invalid number of threads. Must be 1 or more.");
                }
                else break;
            }

            // Get and validate directories
            directories = new string[numThreads];
            for (int i = 0; i < numThreads; i++)
            {
                while (true)
                {
                    Console.Write($"Enter path for directory {i + 1}: ");
                    string dir = Console.ReadLine();

                    if (string.IsNullOrWhiteSpace(dir))
                    {
                        Console.WriteLine("Directory path cannot be empty.");
                    }
                    else if (!Directory.Exists(dir))
                    {
                        Console.WriteLine("Directory does not exist. Try again.");
                    }
                    else
                    {
                        directories[i] = dir;
                        break;
                    }
                }
            }

            // Launch threads for each directory
            Thread[] threads = new Thread[numThreads];
            for (int i = 0; i < numThreads; i++)
            {
                string dir = directories[i];
                threads[i] = new Thread(() => ProcessDirectory(dir));
                threads[i].Start();
            }

            foreach (Thread t in threads)
            {
                t.Join();
            }

            Console.WriteLine("All files processed. Press any key to exit.");
            Console.ReadKey();
        }

        static void ProcessDirectory(string directory)
        {
            string[] files = Directory.GetFiles(directory);
            foreach (var file in files)
            {
                try
                {
                    Console.WriteLine($"Sending file: {file}");
                    SendFile(file); // Your networking logic here
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending file {file}: {ex.Message}");
                }
            }
        }
        static void SendFile(string filePath)
        {
            try
            {
                string originalFileName = Path.GetFileName(filePath);
                byte[] fileNameBytes = Encoding.UTF8.GetBytes(originalFileName);
                int fileNameLength = fileNameBytes.Length;

                // Read the file data and determine original file size.
                byte[] fileData = File.ReadAllBytes(filePath);
                long fileSize = fileData.Length;

                // Compression threshold: 20 MB
                const long threshold = 20 * 1024 * 1024;
                bool compressed = false;
                if (fileSize > threshold)
                {
                    // Call the separate video compression function
                    if (CompressVideo(filePath, out byte[] newFileData, out long newSize))
                    {
                        fileData = newFileData;
                        fileSize = newSize;
                        compressed = true;
                        Console.WriteLine($"\nVideo above 20MB, file {originalFileName} was compressed. New size: {(fileSize / (1024.0 * 1024.0)):0.00} MB");
                    }
                    else
                    {
                        Console.WriteLine($"\nCompression failed for file {originalFileName}. Proceeding without compression.");
                    }
                }

                byte[] fileNameLengthBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(fileNameLength));
                byte[] fileSizeBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(fileSize));

                using (TcpClient client = new TcpClient())
                {
                    client.Connect(consumerIP, consumerPort);
                    using (NetworkStream ns = client.GetStream())
                    {
                        // Send header
                        ns.Write(fileNameLengthBytes, 0, fileNameLengthBytes.Length);
                        ns.Write(fileNameBytes, 0, fileNameBytes.Length);
                        ns.Write(fileSizeBytes, 0, fileSizeBytes.Length);

                        // Send file data immediately
                        ns.Write(fileData, 0, fileData.Length);
                        ns.Flush();

                        // Then wait for a response (e.g., OK or QUEUE_FULL)
                        byte[] responseBuffer = new byte[100];
                        int bytesRead = ns.Read(responseBuffer, 0, responseBuffer.Length);
                        string response = Encoding.UTF8.GetString(responseBuffer, 0, bytesRead);
                        Console.WriteLine($"\nReceived response from consumer: {response}\n");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending file {filePath}: {ex.Message}");
            }
        }
        static bool CompressVideo(string inputPath, out byte[] compressedData, out long newSize)
        {
            // Path to ffmpeg.exe inside the repo structure
            string ffmpegPath = Path.Combine(Directory.GetCurrentDirectory(), "FFMPEG", "bin", "ffmpeg.exe");

            // Check if ffmpeg exists at the specified location
            if (!File.Exists(ffmpegPath))
            {
                compressedData = null;
                newSize = 0;
                return false;
            }

            string tempOutputFile = Path.Combine(Path.GetTempPath(), Path.GetFileName(inputPath));
            var ffmpegProcess = new System.Diagnostics.Process();
            ffmpegProcess.StartInfo.FileName = ffmpegPath;

            // -i "inputPath" : input file.
            // -vcodec libx264 -crf 28: re-encode with H.264 using CRF 28 (lower quality = smaller size).
            // -preset fast: faster encoding.
            // -acodec copy: copy audio without re-encoding.

            string arguments = $"-i \"{inputPath}\" -vcodec libx264 -crf 28 -preset fast -acodec copy \"{tempOutputFile}\"";
            ffmpegProcess.StartInfo.Arguments = arguments;
            ffmpegProcess.StartInfo.CreateNoWindow = true;
            ffmpegProcess.StartInfo.UseShellExecute = false;
            ffmpegProcess.StartInfo.RedirectStandardError = true;
            ffmpegProcess.Start();

            // FFmpeg outputs logs to stderr
            string ffmpegOutput = ffmpegProcess.StandardError.ReadToEnd();
            ffmpegProcess.WaitForExit();

            if (File.Exists(tempOutputFile))
            {
                compressedData = File.ReadAllBytes(tempOutputFile);
                newSize = compressedData.LongLength;
                File.Delete(tempOutputFile);
                return true;
            }
            else
            {
                compressedData = null;
                newSize = 0;
                return false;
            }
        }
    }
}
