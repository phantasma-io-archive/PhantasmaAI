using System.Text;
using LunarLabs.Bots;
using LunarLabs.WebServer.Core;
using LunarLabs.WebServer.HTTP;
using LunarLabs.WebServer.Templates;

namespace Phantasma.AI
{
    internal class Launcher
    {

        private static ServerSettings settings;
        private static ChatGTPBotPlugin chatbot;

        static async Task Main(string[] args)
        {
            settings = ServerSettings.Parse(args);

            var path = settings.Path.Replace("\\", "/");

            if (!path.EndsWith("/")) path += "/";

            Console.WriteLine("Server path: " + path);

            var apiKeyFilePath = Path.GetFullPath(path + "apikey.txt");
            string apiKey = null;

            Console.WriteLine("Searching for " + apiKeyFilePath);

            if (File.Exists(apiKeyFilePath))
            {
                apiKey = File.ReadAllText(apiKeyFilePath).Trim();
            }
            else
            {
                throw new Exception("API KEY file not found!");
            }

            chatbot = ChatGTPBotPlugin.Initialize<SpeckyBot>(path, apiKey);

            LoggerCallback logger = (leve, msg) => { }; // , ConsoleLogger.Write

            var server = new HTTPServer(settings, logger);

            chatbot.Install(server);

            server.Run();

            bool running = true;

            Console.CancelKeyPress += delegate {
                server.Stop();
                running = false;
            };

            while (running)
            {
                Thread.Sleep(500);
            }
        }
    }
}