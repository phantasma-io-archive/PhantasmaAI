using System.Text;
using LunarLabs.Bots;
using LunarLabs.WebServer.Core;
using LunarLabs.WebServer.HTTP;
using LunarLabs.WebServer.Templates;

namespace ChatGPT
{

    internal class Launcher
    {

        private static ServerSettings settings;
        private static ChatGTPBot chatbot;

        static async Task Main(string[] args)
        {
            settings = ServerSettings.Parse(args, prefix: "-");

            var path = settings.Path;
            Console.WriteLine("Server path: " + path);

            var apiKeyFile = path + "apikey.txt";
            string apiKey = null;

            if (File.Exists(apiKeyFile))
            {
                apiKey = File.ReadAllText(apiKeyFile).Trim();
            }

            chatbot = new ChatGTPBot(path, apiKey);

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