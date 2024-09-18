using System;
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

            // Create new capture instance and get the capture device
            NetCapture netCap = new NetCapture(connection);
            var capDevice = netCap.getCaptureDevice();
            netCap.deviceSetup(capDevice);

            Console.WriteLine();
            Console.WriteLine("-- Listening on {0}, hit 'Enter' to stop...",
                capDevice.Description);

            // Start a new thread to decrement weights on connections
            //Thread connectionClearingThread = new Thread(new ThreadStart(netCap.connectionClear));
            //connectionClearingThread.Start();

            // Start capture on main thread
            capDevice.StartCapture();

            // Wait for "enter" to stop capture
            Console.ReadLine();

            // Close cap device
            capDevice.Close();
            //connectionClearingThread.Abort();
        }
    }
}
