using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using OpenAI.GPT3;
using OpenAI.GPT3.Managers;
using OpenAI.GPT3.ObjectModels.RequestModels;
using OpenAI.GPT3.ObjectModels;
using LunarLabs.WebServer.HTTP;
using LunarLabs.WebServer.Templates;

namespace Phantasma.AI
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

    public class Chatbot
    {   
        private string apiKey;

        private string chatLogPath;

        private OpenAIService gpt3;
        private string assistantText;

        private Dictionary<int, List<ChatEntry>> convos = new Dictionary<int, List<ChatEntry>>();
        private HashSet<int> pending = new HashSet<int>();

        public Chatbot(string path, string apiKey)
        {
            if (!path.EndsWith('/'))
            {
                path += "/";
            }

            chatLogPath = path + "Chatlogs/";
            if (!Directory.Exists(chatLogPath))
            {
                Directory.CreateDirectory(chatLogPath);
            }

            if (string.IsNullOrEmpty(apiKey))
            {
                throw new Exception("Please configure API key");
            }
            else
            {
                this.apiKey = apiKey;
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

        public void Install(HTTPServer server, string entryPath = "/")
        {
            var templateEngine = new TemplateEngine(server, "botviews");

            if (!entryPath.EndsWith("/"))
            {
                entryPath += "/";
            }

            server.Get(entryPath, (request) =>
            {
                var id = request.session.GetInt("chat_id");

                if (id == 0)
                {
                    id = GenerateUserID();
                }

                return HTTPResponse.Redirect(entryPath + id);
            });

            server.Get(entryPath + "{chat_id}", (request) =>
            {
                var context = GetContext(request);
                return templateEngine.Render(context, "main");
            });

            server.Get("/convo/{chat_id}", (request) =>
            {
                var context = GetContext(request);
                return templateEngine.Render(context, "convo");
            });

            server.Post("/convo/{chat_id}", (request) =>
            {
                var text = request.GetVariable("message");

                var chat_id = GetChatID(request);

                List<ChatEntry> convo = FindConvo(chat_id);
                lock (convos)
                {
                    convo.Add(new ChatEntry(false, text));
                }

                //var result = Task.Run(() => DoChatRequest(userID, text)).Result; 
                DoChatRequest(chat_id, text);

                var context = GetContext(request);
                return templateEngine.Render(context, "convo");
            });
        }

        const string CHAT_BREAK = "####";

        private string FilterCodeTags(string input, ref bool insideCode)
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

        private List<ChatEntry> BeautifyConvo(List<ChatEntry> convo)
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

        private List<ChatEntry> FindConvo(int chat_id)
        {
            List<ChatEntry> convo;

            lock (convos)
            {
                if (convos.ContainsKey(chat_id))
                {
                    convo = convos[chat_id];
                }
                else
                if (ChatExists(chat_id))
                {
                    var fileName = GetChatLogPath(chat_id);
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

                    convos[chat_id] = convo;
                }
                else
                {
                    convo = new List<ChatEntry>();
                    convo.Add(new ChatEntry(true, "Hello Souldier, what do you want to build today?"));
                    convos[chat_id] = convo;
                }
            }

            return convo;
        }

        private async Task<bool> DoChatRequest(int chat_id, string questionText)
        {
            Console.WriteLine($"CHATGPT.beginRequest({chat_id})");

            lock (pending)
            {
                if (pending.Contains(chat_id))
                {
                    return false;
                }

                pending.Add(chat_id);
            }

            IList<ChatMessage> messages = new List<ChatMessage>();

            messages.Add(new ChatMessage("system", assistantText));

            List<ChatEntry> convo = FindConvo(chat_id);

            foreach (var entry in convo)
            {
                string role = entry.isSpecky ? "assistant" : "user";

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

                var fileName = GetChatLogPath(chat_id);

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
                pending.Remove(chat_id);
            }

            Console.WriteLine($"CHATGPT.endRequest({chat_id})");

            return completionResult.Successful;
        }

        private void LimitTokens(IList<ChatMessage> messages, int tokenLimit)
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

                for (int i = 0; i < messages.Count; i++)
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

        private string GetConvoAsJSON(int chat_id)
        {
            var json = new StringBuilder();
            json.Append('[');

            var entries = FindConvo(chat_id);
            foreach (var entry in entries)
            {
                json.AppendLine($"\"{entry.text}\"");
            }

            json.Append(']');
            return json.ToString();
        }

        private string GetChatLogPath(int chatID)
        {
            return chatLogPath + chatID + ".txt";
        }

        private bool ChatExists(int chat_id)
        {
            var fileName = GetChatLogPath(chat_id);
            return File.Exists(fileName);
        }


        private static Random random = new Random();

        private int GenerateUserID()
        {
            lock (convos)
            {
                int randomID;

                do
                {
                    randomID = 1000 + random.Next() % 8999;

                    if (!ChatExists(randomID))
                    {
                        return randomID;
                    }

                } while (true);
            }
        }

        private int GetChatID(HTTPRequest request)
        {
            int chat_id;

            int.TryParse(request.GetVariable("chat_id"), out chat_id);

            return chat_id;
        }

        private Dictionary<string, object> GetContext(HTTPRequest request)
        {
            var session = request.session;

            var user_id = session.ID.Substring(0, 16);

            var chat_id = GetChatID(request);

            var context = new Dictionary<string, object>();
            context["user_name"] = "Anonymous";
            context["user_id"] = user_id;
            context["chat_id"] = chat_id;
            context["chat"] = BeautifyConvo(FindConvo(chat_id));

            request.session.SetString("user_id", user_id);
            request.session.SetInt("chat_id", chat_id);

            lock (pending)
            {
                context["pending"] = pending.Contains(chat_id);
            }

            return context;
        }


    }
}
