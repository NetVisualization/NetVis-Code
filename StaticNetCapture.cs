using System;
using System.Linq;
using SharpPcap;
using PacketDotNet;
using System.Net;
using System.Net.NetworkInformation;
using MongoDB.Driver;
using System.Threading;
using System.Net.Sockets;

namespace NetCapture
{
    internal class StaticNetCapture
    {
        private IMongoCollection<Packet> packetRecords;
        private IMongoCollection<Node> nodeRecords;
        private IMongoCollection<Connection> connectionRecords;

        private const int DECREMENT_INTERVAL = 30000;

        public StaticNetCapture(DatabaseConn connection)
        {
            packetRecords = connection.getPacketCollection();
            nodeRecords = connection.getNodeCollection();
            connectionRecords = connection.getConnectionCollection();
        }

        public ILiveDevice getCaptureDevice()
        {
            //Print SharpPcap version
            var ver = Pcap.SharpPcapVersion;
            Console.WriteLine("SharpPcap {0}", ver);

            // Retrieve the device list
            var devices = CaptureDeviceList.Instance;

            // If no devices were found print an error
            if (devices.Count() < 1)
            {
                Console.WriteLine("No devices were found on this machine");
                return null;
            }

            Console.WriteLine();
            Console.WriteLine("The following devices are available on this machine:");
            Console.WriteLine("----------------------------------------------------");
            Console.WriteLine();

            // Print out the devices
            int i = 0;
            foreach (var dev in devices)
            {
                //Console.WriteLine("{0}) {1} {2}", i, dev.Name, dev.Description);
                Console.WriteLine("{0}) {1}", i, dev.ToString());
                i++;
            }

            Console.WriteLine();
            Console.Write("-- Please choose a device to capture: ");

            i = int.Parse(Console.ReadLine());

            return devices[i];
        }

        public void deviceSetup(ILiveDevice device)
        {
            // Register our handler function to the 'packet arrival' event
            device.OnPacketArrival += new PacketArrivalEventHandler(device_OnPacketArrival);

            // Open the device for capturing
            //device.Open(DeviceModes.Promiscuous | DeviceModes.DataTransferUdp | DeviceModes.NoCaptureLocal, 1000);
            device.Open(DeviceModes.Promiscuous, 1000);
        }

        private void device_OnPacketArrival(object sender, PacketCapture e)
        {
            // Define a new empty packet record
            Packet packetRecord = new Packet();

            // Define source and dest mac/address/port variables
            PhysicalAddress sourceMAC;
            PhysicalAddress destinationMAC;
            IPAddress sourceIP = null;
            IPAddress destinationIP = null;
            int sourcePort;
            int destinationPort;

            // Parse packet capture into a useful packet
            PacketDotNet.Packet packet = PacketDotNet.Packet.ParsePacket(e.GetPacket().LinkLayerType, e.GetPacket().Data);
            int length = packet.TotalPacketLength;
            Type type = packet.GetType();

            // Convert the timestamp to a readable format "MM-DD-YYYY HH:MM:SS.sss"
            // Yes timestamps as strings is bad, but Mongo can't handle DateTime objects
            DateTime timestamp = e.GetPacket().Timeval.Date;
            string time = timestamp.ToString("MM-dd-yyyy HH:mm:ss.fffff");
            //string time = $"{timestamp.Month}-{timestamp.Day}-{timestamp.Year} {timestamp.Hour}:{timestamp.Minute}:{timestamp.Second}.{timestamp.Millisecond}";

            // Assign known values of the packet record
            packetRecord.Length = length;
            packetRecord.PayloadHex = null; // ByteArrayToString(e.GetPacket().Data);
            packetRecord.TimestampStr = time;

            // Extract MAC information
            EthernetPacket ethernetPacket = packet.Extract<EthernetPacket>();
            if (ethernetPacket != null)
            {
                sourceMAC = ethernetPacket.SourceHardwareAddress;
                destinationMAC = ethernetPacket.DestinationHardwareAddress;

                packetRecord.SourceMAC = sourceMAC.ToString();
                packetRecord.DestinationMAC = destinationMAC.ToString();

                packetRecord.Protocol = "ETHERNET";
            }

            // Extract IP information
            IPPacket ipPacket = packet.Extract<IPPacket>();
            if (ipPacket != null)
            {
                sourceIP = ipPacket.SourceAddress;
                destinationIP = ipPacket.DestinationAddress;

                packetRecord.SourceIP = sourceIP.ToString();
                packetRecord.DestinationIP = destinationIP.ToString();
                packetRecord.Protocol = "IP";
            }

            // Extract Port information
            TcpPacket tcpPacket = packet.Extract<TcpPacket>();
            if (tcpPacket != null)
            {
                sourcePort = tcpPacket.SourcePort;
                destinationPort = tcpPacket.DestinationPort;

                packetRecord.SourcePort = sourcePort;
                packetRecord.DestinationPort = destinationPort;
                packetRecord.Protocol = "TCP";
            }

            UdpPacket udpPacket = packet.Extract<UdpPacket>();
            if (udpPacket != null)
            {
                sourcePort = udpPacket.SourcePort;
                destinationPort = udpPacket.DestinationPort;

                packetRecord.SourcePort = sourcePort;
                packetRecord.DestinationPort = destinationPort;
                packetRecord.Protocol = "UDP";
            }

            if (sourceIP != null && destinationIP != null)
            {
                packetRecords.InsertOne(packetRecord);

                // With the new packet, update the node/connection DBs
                updateNodesConns(packetRecord);
            }
        }

        public void updateNodesConns(Packet p)
        {
            // --- Nodes ---
            var sourceIpAddressFilter = Builders<Node>.Filter.Eq(x => x.IPaddr, p.SourceIP);
            var destIpAddressFilter = Builders<Node>.Filter.Eq(x => x.IPaddr, p.DestinationIP);

            Node sourceNode = nodeRecords.Find(sourceIpAddressFilter).FirstOrDefault<Node>();
            Node destNode = nodeRecords.Find(destIpAddressFilter).FirstOrDefault<Node>();

            if (sourceNode == null)
            {
                sourceNode = new Node();
                sourceNode.MACaddr = p.SourceMAC;
                sourceNode.IPaddr = p.SourceIP;
                sourceNode.DeviceType = null;
                sourceNode.NumConnections = 0;
                sourceNode.NumPackets = 1;
                nodeRecords.InsertOne(sourceNode);
            }
            else
            {
                sourceNode.NumPackets += 1;
                nodeRecords.ReplaceOne(sourceIpAddressFilter, sourceNode);
            }
            if (destNode == null)
            {
                destNode = new Node();
                destNode.MACaddr = p.DestinationMAC;
                destNode.IPaddr = p.DestinationIP;
                destNode.DeviceType = null;
                destNode.NumConnections = 0;
                destNode.NumPackets = 1;
                nodeRecords.InsertOne(destNode);
            }
            else
            {
                destNode.NumPackets += 1;
                nodeRecords.ReplaceOne(destIpAddressFilter, destNode);
            }

            // --- Connections ---
            // Check the database for a matching connection in both directions
            // We need to check both directions so we can capture data travelling along the connection and
            // against it.
            var AtoBDir = Builders<Connection>.Filter.Eq(x => x.NodeA_IP, p.SourceIP)
                & Builders<Connection>.Filter.Eq(x => x.NodeB_IP, p.DestinationIP);
            Connection AtoBConn = connectionRecords.Find(AtoBDir).FirstOrDefault<Connection>();
            
            var BtoADir = Builders<Connection>.Filter.Eq(x => x.NodeA_IP, p.DestinationIP)
                & Builders<Connection>.Filter.Eq(x => x.NodeB_IP, p.SourceIP);
            Connection BtoAConn = connectionRecords.Find(BtoADir).FirstOrDefault<Connection>();

            // If we don't have a connection for this IP pair yet, make a new one
            if (AtoBConn == null && BtoAConn == null)
            {
                Connection newConnection = new Connection();
                newConnection.NodeA_IP = p.SourceIP;
                newConnection.NodeB_IP = p.DestinationIP;
                newConnection.NumPackets = 1;
                newConnection.LastPacketTimestampStr = p.TimestampStr;
                connectionRecords.InsertOne(newConnection);

                // Get the two nodes on either side of the connection and increment their connection counters
                sourceNode.NumConnections++; 
                destNode.NumConnections++;
                nodeRecords.ReplaceOne(sourceIpAddressFilter, sourceNode);
                nodeRecords.ReplaceOne(destIpAddressFilter, destNode);
            }

            // If a connection exists in the DB with conn.source == p.source and conn.dest == p.dest increment the counter
            else if (AtoBConn != null)
            {
                AtoBConn.NumPackets += 1;
                AtoBConn.LastPacketTimestampStr = p.TimestampStr;
                connectionRecords.ReplaceOne(AtoBDir, AtoBConn);
            }

            // If a connection exists in the DB with conn.source == p.dest and conn.dest == p.source increment the counter
            else if (BtoAConn != null)
            {
                BtoAConn.NumPackets += 1;
                BtoAConn.LastPacketTimestampStr = p.TimestampStr;
                connectionRecords.ReplaceOne(BtoADir, BtoAConn);
            }
        }

        public static string ByteArrayToString(byte[] ba)
        {
            return BitConverter.ToString(ba).Replace("-", "");
        }
    }
}
