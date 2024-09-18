using System;
using MongoDB.Driver;

namespace NetCapture
{
    internal class DatabaseConn
    {
        public const int NODE_EXP_SECONDS = 150;
        public const int PACKET_EXP_SECONDS = 30;

        private MongoClient MONGO_DB_CLIENT;
        private IMongoDatabase NET_VIZ_DB;

        // Collections from the database
        private IMongoCollection<Packet> PACKET_COLLECTION;
        private IMongoCollection<Node> NODE_COLLECTION;
        private IMongoCollection<Connection> CONNECTION_COLLECTION;
        private IMongoCollection<History> HISTORY_COLLECTION;

        // TTL index variables
        IndexKeysDefinition<Packet> indexKeysDefinitionP;
        CreateIndexOptions indexOptionsP;
        CreateIndexModel<Packet> indexModelP;

        IndexKeysDefinition<Node> indexKeysDefinitionN;
        CreateIndexOptions indexOptionsN;
        CreateIndexModel<Node> indexModelN;

        public DatabaseConn(string host, string port, string dbName)
        {
            // Connection URI
            // Normal (Does not allow occulus client to use REST API)
            //string connectionUri = $"mongodb://{host}:{port}";

            // Atlas API (Has a REST API for easy access)
            // TODO: FIX SECURITY
            string connectionUri = "mongodb+srv://Admin:Password@atlascluster.3kz6gdp.mongodb.net/?retryWrites=true&w=majority";

            try
            {
                // Create a new client connected to the server
                MONGO_DB_CLIENT = new MongoClient(connectionUri);

                // Get correct database
                NET_VIZ_DB = MONGO_DB_CLIENT.GetDatabase(dbName);

                // Update connection history for debugging
                HISTORY_COLLECTION = NET_VIZ_DB.GetCollection<History>("ConnectionHistory");
                updateHistory();

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
            }
        }

        public void updateHistory()
        {
            // Add a timestamp for every access
            History history = new History();
            history.Timestamp = DateTime.Now.ToString("yyyyMMdd HH:mm:ss");
            history.ConnectedDevice = "Capture Device";

            HISTORY_COLLECTION.InsertOne(history);
        }

        public void createCollections()
        {
            // Known collections created for every capture session
            // Can be modified for multiple client interfaces if necessary
            NET_VIZ_DB.CreateCollection("Packets");
            PACKET_COLLECTION = NET_VIZ_DB.GetCollection<Packet>("Packets");

            NET_VIZ_DB.CreateCollection("Nodes");
            NODE_COLLECTION = NET_VIZ_DB.GetCollection<Node>("Nodes");

            NET_VIZ_DB.CreateCollection("Connections");
            CONNECTION_COLLECTION = NET_VIZ_DB.GetCollection<Connection>("Connections");
        }

        public void destroyCollections()
        {
            // Destroy all collections except for ConnectionHistory
            NET_VIZ_DB.DropCollection("Packets");
            NET_VIZ_DB.DropCollection("Nodes");
            NET_VIZ_DB.DropCollection("Connections");
        }

        public IMongoCollection<Packet> getPacketCollection()
        {
            return PACKET_COLLECTION;
        }

        public IMongoCollection<Node> getNodeCollection()
        {
            return NODE_COLLECTION;
        }

        public IMongoCollection<Connection> getConnectionCollection()
        {
            return CONNECTION_COLLECTION;
        }
    }
}
