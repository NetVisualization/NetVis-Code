using MongoDB.Driver;
using SharpPcap;
using SharpPcap.LibPcap;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;


namespace NetCapture
{
    internal class Driver
    {
        private const string DB_NAME = "NetworkVisualization";
        private const string DB_HOST = "localhost";
        private const string DB_PORT = "27017";

        public static void Main(string[] args)
        {
            // Set up DB connection
            //                                     Unnecessary params - keep in temporarily
            DatabaseConn connection = new DatabaseConn(DB_HOST, DB_PORT, DB_NAME);

            // Destroy collections at end of capture
            connection.destroyCollections();
            connection.createCollections();

            // Ask user how where they want to get data from
            Console.WriteLine("Which source from which source?");
            Console.WriteLine("0) Static Capture from WiFi Network");
            Console.WriteLine("1) Local .pcap file");

            string dataSource = Console.ReadLine();

            // Static Network WiFi Capture
            if (dataSource == "0")
            {
                NetworkStaticCapture(connection);
            }

            // Local pcap file
            else if (dataSource == "1")
            {
                Console.WriteLine("Enter .pcap filename (in NetVis-Code):");
                string fname = Console.ReadLine();

                FileCapture(connection, fname);
            }
            else
            {
                Console.WriteLine("Invalid source. Exiting...");
            }

            // Wait for close
            Console.ReadLine();
        }

        public static void NetworkStaticCapture(DatabaseConn connection)
        {
            // Create new capture instance and get the capture device
            StaticNetCapture netCap = new StaticNetCapture(connection);
            var capDevice = netCap.getCaptureDevice();
            netCap.deviceSetup(capDevice);

            Console.WriteLine();
            Console.WriteLine("-- Listening on {0}, hit 'Enter' to stop...",
                capDevice.Description);

            capDevice.StartCapture();

            // Wait for "enter" to stop capture
            Console.ReadLine();

            capDevice.Close();

            Console.WriteLine("\nSuccessful capture!");
        }

        public static void FileCapture(DatabaseConn connection, string fname)
        {
            LocalFileCapture fCap = new LocalFileCapture(connection);

            CaptureFileReaderDevice reader = fCap.GetReader(fname);
            fCap.ReaderSetup(reader);

            Console.WriteLine("Reading file...");

            reader.Capture();

            reader.Close();

            Console.WriteLine("\nSuccessful read!");
        }
    }
}
