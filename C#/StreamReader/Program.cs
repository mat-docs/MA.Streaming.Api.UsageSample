using Newtonsoft.Json;

namespace Stream.Api.Stream.Reader
{
    internal class Program
    {
        /// <summary>
        /// This Sample code will read live sessions from the Stream API Server and convert them into CSVs.
        /// It can be modified to push data from the Stream API to any other data storage.
        /// </summary>
        private static void Main()
        {
            var config = LoadJson("./Config/Config.json");
            var atlasSessionWriter = new AtlasSessionWriter(config.dbPath);
            var streamApiClient = new StreamApiClient(config.rootPath, atlasSessionWriter, config.dataSource);
            atlasSessionWriter.Initialise();
            streamApiClient.Initialise(config.ipAddress);
            var dataSource = config.dataSource;

            streamApiClient.TryGetLiveSessions(dataSource);

            while (true)
            {
                var task = streamApiClient.SubscribeToStartSessionNotification(dataSource);
                task.Wait();
                Thread.Sleep(5000);
            }
        }

        public static Config LoadJson(string filePath)
        {
            using (StreamReader reader = new StreamReader(filePath))
            {
                string json = reader.ReadToEnd();
                return JsonConvert.DeserializeObject<Config>(json);
            }
        }
    }
}