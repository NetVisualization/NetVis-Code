using MongoDB.Driver;
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace NetCapture
{
    internal class LocalFileCapture
    {
        private IMongoCollection<Packet> packetRecords;
        private IMongoCollection<Node> nodeRecords;
        private IMongoCollection<Connection> connectionRecords;

        private CaptureFileReaderDevice reader;

        public LocalFileCapture(DatabaseConn connection)
        {
            packetRecords = connection.getPacketCollection();
            nodeRecords = connection.getNodeCollection();
            connectionRecords = connection.getConnectionCollection();
        }

        public CaptureFileReaderDevice GetReader(string fname)
        {
            return new CaptureFileReaderDevice("../../" + fname);
        }

        internal void ReaderSetup(CaptureFileReaderDevice reader)
        {
            try
            {
                reader.Open();
                reader.OnPacketArrival += new PacketArrivalEventHandler(device_OnPacketArrival);

            } catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Console.ReadLine();
                System.Environment.Exit(1);
            }
        }

        private void device_OnPacketArrival(object sender, PacketCapture e)
        {
            // Define a new empty packet record
            Packet packetRecord = new Packet();

            // Define source and dest mac/address/port variables
            PhysicalAddress sourceMAC;
            PhysicalAddress destinationMAC;
            IPAddress sourceIP;
            IPAddress destinationIP;
            int sourcePort;
            int destinationPort;

            // Parse packet capture into a useful packet
            PacketDotNet.Packet packet = PacketDotNet.Packet.ParsePacket(e.GetPacket().LinkLayerType, e.GetPacket().Data);
            int length = packet.TotalPacketLength;
            Type type = packet.GetType();

            // Convert the payload to HEX
            // Discuss before deployment
            byte[] payloadData = e.GetPacket().Data;
            string payloadHex = ByteArrayToString(payloadData);

            // Convert the timestamp to a readable format
            var timestamp = e.GetPacket().Timeval.Date;
            string time = $"{timestamp.Month}-{timestamp.Day}-{timestamp.Year} {timestamp.Hour}:{timestamp.Minute}:{timestamp.Second}.{timestamp.Millisecond}";

            // Assign known values of the packet record
            packetRecord.Length = length;
            packetRecord.PayloadHex = payloadHex;
            packetRecord.Timestamp = time;

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
                Console.WriteLine(sourceIP);
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

            packetRecords.InsertOne(packetRecord);

            // With the new packet, update the node/connection DBs
            createNodes(packetRecord);
            createConnection(packetRecord);
        }

        public void createNodes(Packet p)
        {
            var sourceIpAddressFilter = Builders<Node>.Filter.Eq(x => x.IPaddr, p.SourceIP);
            var destIpAddressFilter = Builders<Node>.Filter.Eq(x => x.IPaddr, p.DestinationIP);

            Node sourceNode = nodeRecords.Find(sourceIpAddressFilter).FirstOrDefault<Node>();
            Node destNode = nodeRecords.Find(destIpAddressFilter).FirstOrDefault<Node>();

            // Create or Update Source Node
            if (sourceNode == null)
            {
                Node newNode = new Node();
                newNode.MACaddr = p.SourceMAC;
                newNode.IPaddr = p.SourceIP;
                newNode.DeviceType = null;
                newNode.NumConnections = 1;
                newNode.NumPackets = 1;
                nodeRecords.InsertOne(newNode);
            }
            else
            {
                sourceNode.NumPackets += 1;
                nodeRecords.ReplaceOne(sourceIpAddressFilter, sourceNode);
            }

            // Create or Update Destination Node
            if (destNode == null)
            {
                Node newNode = new Node();
                newNode.MACaddr = p.DestinationMAC;
                newNode.IPaddr = p.DestinationIP;
                newNode.DeviceType = null;
                newNode.NumConnections = 1;
                newNode.NumPackets = 1;
                nodeRecords.InsertOne(newNode);
            }
            else
            {
                destNode.NumPackets += 1;
                nodeRecords.ReplaceOne(destIpAddressFilter, destNode);
            }
        }

        public void createConnection(Packet p)
        {
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
                newConnection.LastPacketTimestamp = p.Timestamp;
                connectionRecords.InsertOne(newConnection);

                var sourceIpAddressFilter = Builders<Node>.Filter.Eq(x => x.IPaddr, p.SourceIP);
                var destIpAddressFilter = Builders<Node>.Filter.Eq(x => x.IPaddr, p.DestinationIP);

                Node sourceNode = nodeRecords.Find(sourceIpAddressFilter).FirstOrDefault<Node>();
                Node destNode = nodeRecords.Find(destIpAddressFilter).FirstOrDefault<Node>();

                sourceNode.NumConnections += 1;
                destNode.NumConnections += 1;
                nodeRecords.ReplaceOne(sourceIpAddressFilter, sourceNode);
            }

            // If a connection exists in the DB with conn.source == p.source and conn.dest == p.dest increment the counter
            else if (AtoBConn != null)
            {
                AtoBConn.NumPackets += 1;
                AtoBConn.LastPacketTimestamp = p.Timestamp;
                connectionRecords.ReplaceOne(AtoBDir, AtoBConn);
            }

            // If a connection exists in the DB with conn.source == p.dest and conn.dest == p.source increment the counter
            else if (BtoAConn != null)
            {
                BtoAConn.NumPackets += 1;
                BtoAConn.LastPacketTimestamp = p.Timestamp;
                connectionRecords.ReplaceOne(BtoADir, BtoAConn);
            }
        }

        public static string ByteArrayToString(byte[] ba)
        {
            return BitConverter.ToString(ba).Replace("-", "");
        }
    }
}
