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
    public class Packet
    {
        public ObjectId Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string SourceMAC { get; set; }
        public string DestinationMAC { get; set; }
        public string SourceIP { get; set; }
        public string DestinationIP { get; set; }
        public int SourcePort { get; set; }
        public int DestinationPort { get; set; }
        public string Protocol { get; set; }
        public int Length { get; set; }
        public string PayloadHex { get; set; }
    }

    public class Connection
    {
        public ObjectId Id { get; set; }
        public string NodeA_IP { get; set; }
        public string NodeB_IP { get; set; }
        public int NumPackets { get; set; }
        public DateTime LastPacketTimestamp {  get; set; }
    }

    public class Node
    {
        public ObjectId Id { get; set; }
        public string MACaddr { get; set; }
        public string IPaddr { get; set; }
        public string DeviceType { get; set; }
        public int NumConnections { get; set; }
        public int NumPackets { get; set; }
    }

    public class History
    {
        public ObjectId Id { get; set; }
        public string Timestamp { get; set; }
        public string ConnectedDevice { get; set; }
    }

}
