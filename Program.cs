using System.Text;

using LunarLabs.WebServer.Core;
using LunarLabs.WebServer.HTTP;
using LunarLabs.WebServer.Templates;

using OpenAI.GPT3;
using OpenAI.GPT3.Managers;
using OpenAI.GPT3.ObjectModels.RequestModels;
using OpenAI.GPT3.ObjectModels;

namespace ChatGPT
{
    public struct ChatEntry
    {
        public readonly bool isSpecky;
        public readonly string text;

        public ChatEntry(bool isSpecky, string text)
        {
            this.isSpecky = isSpecky;
            this.text = text;
        }

        public override string ToString()
        {
            return text;
        }
    }

    internal class Program
    {
        // Declare the API key
        private static string apiKey = null;

        private static string chatLogPath;

        private static ServerSettings settings;

        private static OpenAIService gpt3;
        private static string assistantText;

        private static Dictionary<string, List<ChatEntry>> convos = new Dictionary<string, List<ChatEntry>>();
        private static HashSet<string> pending = new HashSet<string>();

        static async void InitChatGPT(string path)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                if (File.Exists("api.txt"))
                {
                    apiKey = File.ReadAllText("api.txt").Trim();
                }

                if (string.IsNullOrEmpty(apiKey))
                {
                    throw new Exception("Please configure API key");
                }
            }

            // Create an instance of the OpenAIService class
            gpt3 = new OpenAIService(new OpenAiOptions()
            {
                ApiKey = apiKey
            });

            assistantText = File.ReadAllText(path + "assistant.txt");

            if (string.IsNullOrEmpty(assistantText))
            {
                throw new Exception("Could not load assistant data");
            }
        }

        const string CHAT_BREAK = "####";

        static string FilterCodeTags(string input, ref bool insideCode)
        {
            var sb = new StringBuilder();

            var prev1 = '\0';
            var prev2 = '\0';

            foreach (var ch in input)
            {
                if (ch == '`')
                {
                    if (prev2 == ch && prev1 == ch)
                    {
                        insideCode = !insideCode;

                        if (insideCode)
                        {
                            sb.AppendLine("</p><pre>");
                        }
                        else
                        {
                            sb.AppendLine("</pre><p>");
                        }

                    }
                }
                else
                switch (ch)
                {
                        case '<': sb.Append("&lt;"); break;
                        case '>': sb.Append("&gt;"); break;
                        case '&': sb.Append("&amp;"); break;
                        case '"': sb.Append("&quot;"); break;
                        case '\'': sb.Append("&#39;"); break;

                        default:
                            sb.Append(ch); break;
                }

                prev1 = prev2;
                prev2 = ch;
            }

            return sb.ToString();
        }

        static List<ChatEntry> BeautifyConvo(List<ChatEntry> convo)
        {
            var result = new List<ChatEntry>();

            bool insideCode = false;
            foreach (var entry in convo)
            {
                var wasInsideCode = insideCode;

                var lines = entry.text.Split('\n');

                var sb = new StringBuilder();

                foreach (var line in lines)
                {
                    var text = FilterCodeTags(line, ref insideCode);

                    sb.Append(text);

                    if (insideCode)
                    {
                        sb.Append("\n");
                    }
                    else
                    {
                        sb.Append("<br>");
                    }
                }

                var output = sb.ToString(); //.Replace("<br><br>", "<br>");
                result.Add(new ChatEntry(entry.isSpecky, output));
            }

            return result;
        }

        static List<ChatEntry> FindConvo(string userID)
        {
            List<ChatEntry> convo;

            lock (convos)
            {
                if (convos.ContainsKey(userID))
                {
                    convo = convos[userID];
                }
                else
                if (ChatExists(userID))
                {
                    var fileName = GetChatLogPath(userID);
                    var lines = File.ReadAllLines(fileName);

                    convo = new List<ChatEntry>();

                    var sb = new StringBuilder();

                    bool waitingForUserType = true;
                    bool isSpecky = false;

                    foreach (var line in lines)
                    {
                        if (line.StartsWith(CHAT_BREAK))
                        {
                            if (sb.Length > 0)
                            {
                                convo.Add(new ChatEntry(isSpecky, sb.ToString()));
                                sb.Clear();
                            }

                            waitingForUserType = true;
                        }
                        else
                        if (waitingForUserType)
                        {
                            isSpecky = line.StartsWith("specky:");
                            waitingForUserType = false;
                        }
                        else
                        {
                            sb.AppendLine(line);
                        }
                    }

                    if (sb.Length > 0)
                    {
                        convo.Add(new ChatEntry(isSpecky, sb.ToString()));
                    }

                    convos[userID] = convo;
                }
                else
                {
                    convo = new List<ChatEntry>();
                    convo.Add(new ChatEntry(true, "Hello Souldier, what do you want to build today?"));
                    convos[userID] = convo;
                }
            }

            return convo;
        }

        static async Task<bool> DoChatRequest(string userID, string questionText)
        {
            lock (pending)
            {
                if (pending.Contains(userID))
                {
                    return false;
                }

                pending.Add(userID);
            }

            IList<ChatMessage> messages = new List<ChatMessage>();

            messages.Add(new ChatMessage("system", assistantText));

            List<ChatEntry> convo = FindConvo(userID);

            foreach (var entry in convo)
            {
                string role = entry.isSpecky? "assistant" :"user";

                messages.Add(new ChatMessage(role, entry.text));
            }

            // https://platform.openai.com/docs/models/gpt-3-5
            LimitTokens(messages, 4000);

            Console.WriteLine("Sending ChatGTP request...");
            // Create a chat completion request
            var completionResult = await gpt3.ChatCompletion.CreateCompletion(
                                    new ChatCompletionCreateRequest()
                                    {
                                        Messages = messages,
                                        Model = Models.ChatGpt3_5Turbo,
                                        Temperature = 0.5f,
                                        MaxTokens = 500,
                                        N = 1,
                                    });

            // Check if the completion result was successful and handle the response
            Console.WriteLine("Got ChatGTP answer...");
            if (completionResult.Successful)
            {
                var lines = new List<string>();
                lines.Add("user:");
                lines.Add(questionText);
                lines.Add(CHAT_BREAK);

                lines.Add("specky:");
                foreach (var choice in completionResult.Choices)
                {
                    var answer = choice.Message.Content;

                    answer = answer.Replace("```csharp", "```");

                    lines.Add(answer);
                    convo.Add(new ChatEntry(true, answer));
                }
                lines.Add(CHAT_BREAK);

                var fileName = GetChatLogPath(userID);

                File.AppendAllLines(fileName, lines);
            }
            else
            {
                if (completionResult.Error == null)
                {
                    throw new Exception("Unknown Error");
                }
                Console.WriteLine($"{completionResult.Error.Code}:{completionResult.Error.Message}");
            }


            lock (pending)
            {
                pending.Remove(userID);
            }

            return completionResult.Successful;
        }

        private static void LimitTokens(IList<ChatMessage> messages, int tokenLimit)
        {
            int discardedCount = 0;

            do
            {
                int charCount = 0;
                foreach (var msg in messages)
                {
                    charCount += msg.Content.Length;
                }

                var tokenCount = charCount / 3; // approximation

                if (tokenCount <= tokenLimit)
                {
                    break;
                }

                for (int i=0; i<messages.Count; i++)
                {
                    if (messages[i].Role != "system")
                    {
                        discardedCount += messages[i].Content.Length;
                        messages.RemoveAt(i);
                        break;
                    }
                }

            } while (true);

            if (discardedCount > 0)
            {
                Console.WriteLine($"Context pruned, {discardedCount} characters discarded");
            }
        }

        static string GetConvoAsJSON(string userID)
        {
            var json = new StringBuilder();
            json.Append('[');

            var entries = FindConvo(userID);
            foreach (var entry in entries)
            {
                json.AppendLine($"\"{entry.text}\"");
            }

            json.Append(']');
            return json.ToString();
        }

        static string GetChatLogPath(string userID)
        {
            return chatLogPath + userID + ".txt";
        }

        static bool ChatExists(string userID)
        {
            var fileName = GetChatLogPath(userID); 
            return File.Exists(fileName);
        }


        static Random random = new Random();

        static string GenerateUserID()
        {
            lock (convos)
            {
                int randomID;

                do
                {
                    randomID = 1000 + random.Next() % 8999;
                    var user_id = "ghost_" + randomID;

                    if (!ChatExists(user_id))
                    {
                        return user_id;
                    }

                } while (true);
            }
        }

        static Dictionary<string, object> GetContext(HTTPRequest request)
        {
            var session = request.session;

            var user_id = session.ID.Substring(0, 16);

            Console.WriteLine(session.ID);

            var context = new Dictionary<string, object>();
            context["user_name"] = "Anonymous";
            context["user_id"] = user_id;
            context["chat"] = BeautifyConvo(FindConvo(user_id));

            lock (pending)
            {
                context["pending"] = pending.Contains(user_id);
            }

            return context;
        }

        static async Task Main(string[] args)
        {
            settings = ServerSettings.Parse(args, prefix: "-");

            var path = settings.Path;
            Console.WriteLine("Server path: " + path);

            chatLogPath = path + "Chatlogs/";
            if (!Directory.Exists(chatLogPath))
            {
                Directory.CreateDirectory(chatLogPath);
            }

            InitChatGPT(path);

            var server = new HTTPServer(settings, ConsoleLogger.Write);

            var templateEngine = new TemplateEngine(server, "views");

            server.Get("/", (request) =>
            {
                var context = GetContext(request);
                return templateEngine.Render(context, "main");
            });

            server.Get("/chat/{ID}", (request) =>
            {
                var context = GetContext(request);
                return templateEngine.Render(context, "convo");
            });

            server.Post("/chat/{ID}", (request) =>
            {
                var text = request.GetVariable("message");

                var userID = request.GetVariable("ID");

                List<ChatEntry> convo = FindConvo(userID);
                lock (convos)
                {
                    convo.Add(new ChatEntry(false, text));
                }

                //var result = Task.Run(() => DoChatRequest(userID, text)).Result; 
                DoChatRequest(userID, text);

                var context = GetContext(request);
                return templateEngine.Render(context, "convo");
            });

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