using MongoDB.Driver;
using SharpPcap;
using SharpPcap.LibPcap;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Org.BouncyCastle.Asn1.IsisMtt.X509;


namespace NetCapture
{
    internal class Driver
    {
        private const string DB_NAME = "net";
        private const string DB_HOST = "10.200.1.13";
        private const string DB_PORT = "8123";
        private const string DB_USER = "capstone";
        private const string DB_PASS = "boogle";

        public static void Main(string[] args)
        {
            // Set up DB connection
            // Unnecessary params - keep in temporarily
            var db = new DatabaseConn();
            db.connect(DB_HOST, DB_PORT, DB_NAME, DB_USER, DB_PASS);
            var version = db.ExecuteCommand("SELECT version()").Result;
            Console.WriteLine($"Connected to ClickHouse version: {version}");

            // Destroy collections at end of capture
            // TODO: create tables
            // connection.destroyCollections();
            // connection.createCollections();

            // Ask user how where they want to get data from
            Console.WriteLine("Which source from which source?");
            Console.WriteLine("0) Static Capture from WiFi Network");
            Console.WriteLine("1) Local .pcap file");

            string dataSource = Console.ReadLine();

            // Static Network WiFi Capture
            if (dataSource == "0")
            {
                NetworkStaticCapture(db);
            }

            // Local pcap file
            else if (dataSource == "1")
            {
                Console.WriteLine("Enter .pcap filename (in NetVis-Code):");
                string fname = Console.ReadLine();

                FileCapture(db, fname);
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
            //    // Create new capture instance and get the capture device
            //    StaticNetCapture netCap = new StaticNetCapture(connection);
            //    var capDevice = netCap.getCaptureDevice();
            //    netCap.deviceSetup(capDevice);

            //    Console.WriteLine();
            //    Console.WriteLine("-- Listening on {0}, hit 'Enter' to stop...",
            //        capDevice.Description);

            //    capDevice.StartCapture();

            //    // Wait for "enter" to stop capture
            //    Console.ReadLine();

            //    capDevice.Close();

            //    Console.WriteLine("\nSuccessful capture!");
            return;
        }

        public static void FileCapture(DatabaseConn connection, string fname)
        {
            //    LocalFileCapture fCap = new LocalFileCapture(connection);

            //    CaptureFileReaderDevice reader = fCap.GetReader(fname);
            //    fCap.ReaderSetup(reader);

            //    Console.WriteLine("Reading file...");

            //    reader.Capture();

            //    reader.Close();

            //    Console.WriteLine("\nSuccessful read!");
            return;
        }
    }
}
