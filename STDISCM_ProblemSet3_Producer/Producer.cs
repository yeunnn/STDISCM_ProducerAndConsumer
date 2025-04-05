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
            // Prompt for Consumer server IP and port
            Console.Write("Enter Consumer IP: ");
            consumerIP = Console.ReadLine();
            Console.Write("Enter Consumer Port: ");
            if (!int.TryParse(Console.ReadLine(), out consumerPort))
            {
                Console.WriteLine("Invalid port number.");
                return;
            }

            // Prompt for the number of producer threads (directories)
            Console.Write("Enter number of producer threads (directories): ");
            int numThreads;
            if (!int.TryParse(Console.ReadLine(), out numThreads) || numThreads < 1)
            {
                Console.WriteLine("Invalid number.");
                return;
            }
            directories = new string[numThreads];
            for (int i = 0; i < numThreads; i++)
            {
                Console.Write($"Enter path for directory {i + 1}: ");
                directories[i] = Console.ReadLine();
            }

            // Start one thread per directory
            Thread[] threads = new Thread[directories.Length];
            for (int i = 0; i < directories.Length; i++)
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
            if (!Directory.Exists(directory))
            {
                Console.WriteLine($"Directory {directory} does not exist.");
                return;
            }

            string[] files = Directory.GetFiles(directory);
            foreach (var file in files)
            {
                try
                {
                    Console.WriteLine($"Sending file: {file}");
                    SendFile(file);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending file {file}: {ex.Message}");
                }
            }
        }

        static void SendFile(string filePath)
        {
            // Get file name as UTF8 bytes
            byte[] fileNameBytes = Encoding.UTF8.GetBytes(Path.GetFileName(filePath));
            int fileNameLength = fileNameBytes.Length;
            // Read entire file
            byte[] fileData = File.ReadAllBytes(filePath);
            long fileSize = fileData.Length;

            // Prepare header:
            // 4 bytes: file name length (network order)
            byte[] fileNameLengthBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(fileNameLength));
            // 8 bytes: file size (network order)
            byte[] fileSizeBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(fileSize));

            using (TcpClient client = new TcpClient())
            {
                client.Connect(consumerIP, consumerPort);
                using (NetworkStream ns = client.GetStream())
                {
                    // Send header then file bytes
                    ns.Write(fileNameLengthBytes, 0, fileNameLengthBytes.Length);
                    ns.Write(fileNameBytes, 0, fileNameBytes.Length);
                    ns.Write(fileSizeBytes, 0, fileSizeBytes.Length);
                    ns.Write(fileData, 0, fileData.Length);
                }
            }
        }
    }
}
