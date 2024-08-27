using MongoDB.Bson;
using System;
using System.Net;
using System.Net.NetworkInformation;

namespace NetCapture
{
    /**
     * Packet
     * Provides the structure of packets which are stored in the database
     */
    public class PacketRecord
    {
        public ObjectId Id { get; set; }
        public string Timestamp { get; set; }
        public DateTime Expiration { get; set; }
        public string SourceMAC { get; set; }
        public string DestinationMAC { get; set; }
        public string SourceIp { get; set; }
        public string DestinationIp { get; set; }
        public int SourcePort { get; set; }
        public int DestinationPort { get; set; }
        public int Length { get; set; }
        public string PayloadHex { get; set; }
    }

    public class Connection
    {
        public ObjectId Id { get; set; }
        public string MACAddress1 { get; set; }
        public string MACAddress2 { get; set; }
        public int Weight { get; set; }
    }

    public class Node
    {
        public ObjectId Id { get; set; }
        public DateTime Expiration { get; set; }
        public string MACAddress { get; set; }
        public string IPAddress { get; set; }
        public bool HasConnections { get; set; }
    }

    public class History
    {
        public ObjectId Id { get; set; }
        public string Timestamp { get; set; }
        public string ConnectedDevice { get; set; }
    }

}
