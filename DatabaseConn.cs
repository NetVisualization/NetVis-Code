using System;
using System.Threading.Tasks;
using ClickHouse.Client.ADO;
using ClickHouse.Client.Utility;

namespace NetCapture
{
    internal class DatabaseConn
    {
        public const int NODE_EXP_SECONDS = 150;
        public const int PACKET_EXP_SECONDS = 30;

        private ClickHouseConnection _connection;

        // Collections from the database
        //private IMongoCollection<Packet> PACKET_COLLECTION;
        //private IMongoCollection<Node> NODE_COLLECTION;d
        //private IMongoCollection<Connection> CONNECTION_COLLECTION;
        //private IMongoCollection<History> HISTORY_COLLECTION;

        // TTL index variables
        //IndexKeysDefinition<Packet> indexKeysDefinitionP;
        //CreateIndexOptions indexOptionsP;
        //CreateIndexModel<Packet> indexModelP;

        //IndexKeysDefinition<Node> indexKeysDefinitionN;
        //CreateIndexOptions indexOptionsN;
        //CreateIndexModel<Node> indexModelN;

        public void connect(string host, string port, string dbName, string user, string pass)
        {
            // clickhouse connection info
            string connectionString = $"Host={host};Database={dbName};port={port};Username={user};Password={pass}";
            _connection = new ClickHouseConnection(connectionString);
            _connection.Open();
        }
        public async Task<object> ExecuteCommand(string text)
        {
            var cmd = _connection.CreateCommand();
            cmd.CommandText = text;
            return await cmd.ExecuteScalarAsync();
        }

        public void updateHistory()
        {
            // Add a timestamp for every access
            //History history = new History();
            //history.Timestamp = DateTime.Now.ToString("yyyyMMdd HH:mm:ss");
            //history.ConnectedDevice = "Capture Device";

            //HISTORY_COLLECTION.InsertOne(history);'
            return;
        }

        public void createCollections()
        {
            // Known collections created for every capture session
            // Can be modified for multiple client interfaces if necessary
            //NET_VIZ_DB.CreateCollection("Packets");
            //PACKET_COLLECTION = NET_VIZ_DB.GetCollection<Packet>("Packets");

            //NET_VIZ_DB.CreateCollection("Nodes");
            //NODE_COLLECTION = NET_VIZ_DB.GetCollection<Node>("Nodes");

            //NET_VIZ_DB.CreateCollection("Connections");
            //CONNECTION_COLLECTION = NET_VIZ_DB.GetCollection<Connection>("Connections");
            return;
        }

        public void destroyCollections()
        {
            // Destroy all collections except for ConnectionHistory
            //NET_VIZ_DB.DropCollection("Packets");
            //NET_VIZ_DB.DropCollection("Nodes");
            //NET_VIZ_DB.DropCollection("Connections");
            return;
        }

        //public IMongoCollection<Packet> getPacketCollection()
        //{
        //    return PACKET_COLLECTION;
        //}

        //public IMongoCollection<Node> getNodeCollection()
        //{
        //    return NODE_COLLECTION;
        //}

        //public IMongoCollection<Connection> getConnectionCollection()
        //{
        //    return CONNECTION_COLLECTION;
        //}
    }
}