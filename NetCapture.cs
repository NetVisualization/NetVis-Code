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
    internal class NetCapture
    {
        private IMongoCollection<PacketRecord> packetRecords;
        private IMongoCollection<Node> nodeRecords;
        private IMongoCollection<Connection> connectionRecords;

        private const int DECREMENT_INTERVAL = 5000000;
        private const int MAX_CONNECTION_STRENGTH = 100;

        public NetCapture(DatabaseConn connection)
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

            int i = 0;
            // Print out the devices
            foreach (var dev in devices)
            {
                /* Description */
                Console.WriteLine("{0}) {1} {2}", i, dev.Name, dev.Description);
                i++;
            }
            Console.WriteLine();
            Console.Write("-- Please choose a device to capture: ");

            // TODO: FIX SECURITY
            i = int.Parse(Console.ReadLine());

            return devices[i];
        }

        public void deviceSetup(ILiveDevice device)
        {
            // Register our handler function to the 'packet arrival' event
            device.OnPacketArrival += new PacketArrivalEventHandler(device_OnPacketArrival);

            // Open the device for capturing
            int readTimeoutMilliseconds = 1000;
            device.Open(mode: DeviceModes.Promiscuous | DeviceModes.DataTransferUdp | DeviceModes.NoCaptureLocal, read_timeout: readTimeoutMilliseconds);
        }

        private void device_OnPacketArrival(object sender, PacketCapture e)
        {
            // Define a new empty packet record
            PacketRecord packetRecord = new PacketRecord();

            // Define source and dest mac/address/port variables
            PhysicalAddress sourceMac;
            PhysicalAddress destinationMac;
            IPAddress sourceIp;
            IPAddress destinationIp;
            int sourcePort;
            int destinationPort;

            // Parse packet capture into a useful packet
            Packet packet = Packet.ParsePacket(e.GetPacket().LinkLayerType, e.GetPacket().Data);
            int length = packet.TotalPacketLength;
            Type type = packet.GetType();

            // Convert the payload to HEX
            byte[] payloadData = e.GetPacket().Data;

            string payloadHex = ByteArrayToString(payloadData);

            // Convert the timestamp to a readable format
            var timestamp = e.GetPacket().Timeval.Date;
            string time = $"{timestamp.Hour}:{timestamp.Minute}:{timestamp.Second},{timestamp.Millisecond}";

            // Assign known values of the packet record
            packetRecord.Expiration = DateTime.UtcNow.AddSeconds(DatabaseConn.PACKET_EXP_SECONDS);
            packetRecord.Timestamp = time;
            packetRecord.Length = length;
            packetRecord.PayloadHex = payloadHex;

            // Extract MAC information
            EthernetPacket ethernetPacket = packet.Extract<EthernetPacket>();
            if (ethernetPacket != null)
            {
                sourceMac = ethernetPacket.SourceHardwareAddress;
                destinationMac = ethernetPacket.DestinationHardwareAddress;

                packetRecord.SourceMAC = sourceMac.ToString();
                packetRecord.DestinationMAC = destinationMac.ToString();
            }

            // Extract IP information
            IPPacket ipPacket = packet.Extract<IPPacket>();
            if (ipPacket != null)
            {
                sourceIp = ipPacket.SourceAddress;
                destinationIp = ipPacket.DestinationAddress;

                packetRecord.SourceIp = sourceIp.ToString();
                packetRecord.DestinationIp = destinationIp.ToString();
            }

            // Extract Port information
            TcpPacket tcpPacket = packet.Extract<TcpPacket>();
            if (tcpPacket != null)
            {
                sourcePort = tcpPacket.SourcePort;
                destinationPort = tcpPacket.DestinationPort;

                packetRecord.SourcePort = sourcePort;
                packetRecord.DestinationPort = destinationPort;
            }

            UdpPacket udpPacket = packet.Extract<UdpPacket>();
            if (udpPacket != null)
            {
                sourcePort = udpPacket.SourcePort;
                destinationPort = udpPacket.DestinationPort;

                packetRecord.SourcePort = sourcePort;
                packetRecord.DestinationPort = destinationPort;
            }

            packetRecords.InsertOne(packetRecord);
            createNodes(packetRecord);
            createConnection(packetRecord);

        }

        public void createNodes(PacketRecord p)
        {
            var sourceMACAddressFilter = Builders<Node>.Filter.Eq(x => x.MACAddress, p.SourceMAC);
            var destMACAddressFilter = Builders<Node>.Filter.Eq(x => x.MACAddress, p.DestinationMAC);


            Node sourceNode = nodeRecords.Find(sourceMACAddressFilter).FirstOrDefault<Node>();
            Node destNode = nodeRecords.Find(destMACAddressFilter).FirstOrDefault<Node>();

            // Create or Update Source Node
            if (sourceNode == null)
            {
                Node newNode = new Node();
                newNode.MACAddress = p.SourceMAC;
                newNode.IPAddress = p.SourceIp;
                newNode.HasConnections = true;
                newNode.Expiration = DateTime.UtcNow.AddSeconds(DatabaseConn.NODE_EXP_SECONDS);
                nodeRecords.InsertOne(newNode);
            }
            else
            {
                sourceNode.Expiration = DateTime.UtcNow.AddSeconds(DatabaseConn.NODE_EXP_SECONDS);
                sourceNode.IPAddress = p.SourceIp;
                nodeRecords.ReplaceOne(sourceMACAddressFilter, sourceNode);
            }

            // Create or Update Destination Node
            if (destNode == null)
            {
                Node newNode = new Node();
                newNode.MACAddress = p.DestinationMAC;
                newNode.IPAddress = p.DestinationIp;
                newNode.HasConnections = true;
                newNode.Expiration = DateTime.UtcNow.AddSeconds(DatabaseConn.NODE_EXP_SECONDS);
                nodeRecords.InsertOne(newNode);
            }
            else
            {
                destNode.Expiration = DateTime.UtcNow.AddSeconds(DatabaseConn.NODE_EXP_SECONDS);
                destNode.IPAddress = p.DestinationIp;
                nodeRecords.ReplaceOne(destMACAddressFilter, destNode);
            }

            /**
             * 
             * TODO:
             * Does not handle 0xFF broadcast
             * Decide how to display and handle broadcast nodes
             * 
             */
        }

        public void createConnection(PacketRecord p)
        {
            var sourceDestDirection = Builders<Connection>.Filter.Eq(x => x.MACAddress1, p.SourceMAC)
                & Builders<Connection>.Filter.Eq(x => x.MACAddress2, p.DestinationMAC)
                & Builders<Connection>.Filter.Lt(x => x.Weight, MAX_CONNECTION_STRENGTH);
            var
                destSourceDirection = Builders<Connection>.Filter.Eq(x => x.MACAddress1, p.DestinationMAC)
                & Builders<Connection>.Filter.Eq(x => x.MACAddress2, p.SourceMAC)
                & Builders<Connection>.Filter.Lt(x => x.Weight, MAX_CONNECTION_STRENGTH);


            Connection sourceDestConnection = connectionRecords.Find(sourceDestDirection).FirstOrDefault<Connection>();
            Connection destSourceConnection = connectionRecords.Find(destSourceDirection).FirstOrDefault<Connection>();

            if (sourceDestConnection == null && destSourceConnection == null)
            {
                Connection newConnection = new Connection();
                newConnection.MACAddress1 = p.SourceMAC;
                newConnection.MACAddress2 = p.DestinationMAC;
                newConnection.Weight = 1;
                connectionRecords.InsertOne(newConnection);
            }
            else if (sourceDestConnection != null)
            {
                int weight = sourceDestConnection.Weight;
                weight = weight + 1;
                sourceDestConnection.Weight = weight;
                connectionRecords.ReplaceOne(sourceDestDirection, sourceDestConnection);
            }
            else if (destSourceConnection != null)
            {
                int weight = destSourceConnection.Weight;
                weight = weight + 1;
                destSourceConnection.Weight = weight;
                connectionRecords.ReplaceOne(destSourceDirection, destSourceConnection);
            }
        }

        public void connectionClear()
        {
            while (true)
            {
                // Perform op after every 5 seconds
                Thread.Sleep(DECREMENT_INTERVAL);

                // Define the update filter (optional) to match specific documents
                var filter = Builders<Connection>.Filter.Empty;

                // Define the update definition to decrement the field
                var updateDefinition = Builders<Connection>.Update.Inc(x => x.Weight, -1);

                // Update all documents in the collection matching the filter
                var updateResult = connectionRecords.UpdateMany(filter, updateDefinition);

                // Define a condition to check if the field becomes negative
                var condition = Builders<Connection>.Filter.Lt(x => x.Weight, 1);

                // Find documents that meet the condition
                var documentsToDelete = connectionRecords.Find(condition).ToList();

                if (documentsToDelete.Count > 0)
                {
                    // Delete the documents that meet the condition
                    var deleteFilter = Builders<Connection>.Filter.In(x => x.Id, documentsToDelete.Select(doc => doc.Id));
                    connectionRecords.DeleteMany(deleteFilter);
                }
            }
        }

        public static string ByteArrayToString(byte[] ba)
        {
            return BitConverter.ToString(ba).Replace("-", "");
        }
    }
}
