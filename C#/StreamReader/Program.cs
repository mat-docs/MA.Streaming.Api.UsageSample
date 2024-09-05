// <copyright file="Program.cs" company="McLaren Applied Ltd.">
// Copyright (c) McLaren Applied Ltd.</copyright>

using Newtonsoft.Json;

using Stream.Api.Stream.Reader.SqlRace;

namespace Stream.Api.Stream.Reader
{
    internal static class Program
    {
        public static Config? LoadJson(string filePath)
        {
            using var reader = new StreamReader(filePath);
            var json = reader.ReadToEnd();
            return JsonConvert.DeserializeObject<Config>(json);
        }

        /// <summary>
        ///     This Sample code will read live sessions from the Stream API Server and convert them into SQL Race Sessions.
        ///     It can be modified to push data from the Stream API to any other data storage.
        /// </summary>
        private static void Main()
        {
            var config = LoadJson("./Config/Config.json");
            if (config == null)
            {
                Console.WriteLine("No config is found at /Config/Config.json. Closing.");
                return;
            }

            Console.WriteLine("To start press enter");
            Console.ReadLine();
            
            // adding run option fetch class 
            // applying the option 
            var atlasSessionWriter = new AtlasSessionWriter(config.SQLRaceConnectionString);
            var streamApiClient = new StreamApiClient(config);
            var sessionManager = new SessionManagement(streamApiClient, atlasSessionWriter);
            atlasSessionWriter.Initialise();
            streamApiClient.Initialise();
            sessionManager.GetLiveSessions();

            Console.WriteLine("Press Enter to exit application.");
            Console.ReadLine();
            sessionManager.CloseAllSessions();
            atlasSessionWriter.StopRecorderAndServer();
        }
    }
}