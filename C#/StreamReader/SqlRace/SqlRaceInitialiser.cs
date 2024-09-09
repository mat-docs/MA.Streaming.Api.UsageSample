

using MESL.SqlRace.Domain;
using System.Net;

namespace Stream.Api.Stream.Reader.SqlRace
{
    public static class SqlRaceInitialiser
    {
        public static void Initialise()
        {
            Console.WriteLine("Initializing");
            if (!Core.IsInitialized)
            {
                Console.WriteLine("Initializing SQL Race.");
                Core.LicenceProgramName = "SQLRace";
                Core.Initialize();
                Console.WriteLine("SQL Race Initialized.");
            }
            Core.ConfigureServer(true, IPEndPoint.Parse($"127.0.0.1:7300"));
            var sessionManager = SessionManager.CreateSessionManager();
            if (!sessionManager.ServerListener.IsRunning)
            {
                Console.WriteLine("Starting Server Listener at 127.0.0.1:7300.");
                sessionManager.ServerListener.Start();
            }
            else
            {
                Console.WriteLine("Server Listener already started at 127.0.0.1:7300.");
            }
        }

        public static void Shutdown()
        {
            var sessionManager = SessionManager.CreateSessionManager();
            sessionManager.ServerListener.Stop();
        }
    }
}
