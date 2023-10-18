using LunarLabs.Bots;
using System.Text;

namespace Phantasma.AI
{
    public enum SpeckyState
    {
        Init,
        Free,
        DevMode,
        CommunityMode,
        SupportMode,
        GameEnginesMode,
        EducationMode,
    }

    public enum InitialTopics
    {
        I_Want_To_Build,
        Questions_About_Phantasma,
        Customer_Support,
    }

    public enum DevTopics
    {
        Gaming_And_Virtual_Worlds,
        Decentralized_Finance,
        Gambling,
        Social_Media_And_Content_Platforms,
        Marketplaces_And_NFTs_Platforms,
        Supply_Chain_And_Authentication,
        Health_And_Wellness,
        Education_And_Training,
        Wallets_And_Explorers,
        Other,
    }

    public enum GameEngineTopics
    {
        Unity,
        Unreal,
        Godot,
        Other,
    }

    public enum EducationTopics
    {
        Decentralized_Course_Platforms,
        Secure_Exam_And_Certification_Systems,
        Interactive_Learning_Platforms,
        Decentralized_Knowledge_Bases,
        Skill_Tokenization,
        Collaborative_Research_Platforms,    
    }

    public enum MiscTopics
    {
        Event_Ticketing,
        Decentralized_Voting_Systems,
        Personal_Data_Lockers,
        Decentralized_Identity_Systems,
        Crowdfunding_Platforms,
        Travel_And_Tourism,
        Decentralized_Job_Marketplaces,
    }

    [Flags]
    public enum PhantasmaTools
    {
        None = 0,
        PhantasmaLink = 0x1,
        TOMB = 0x2,
        GameEngine = 0x4,
        Wallet = 0x8,
        HTML = 0x10,
        Storage = 0x20,
    }

    public class SpeckyBot : SmartBot
    {
        private SpeckyState state;

        private static Dictionary<Enum, string> _knowledgeBase = null;

        private string _currentRole;
        private GameEngineTopics _currentEngine;
        private PhantasmaTools _currentTools;

        public SpeckyBot(int chat_id, string path) : base(chat_id, path)
        {
            state = SpeckyState.Init;
            
            _currentTools = PhantasmaTools.None;
            _currentEngine = GameEngineTopics.Other;

            LoadInitialState();
            
            if (_knowledgeBase == null || convo.Count <= 2)
            {
                InitKnowledgeBase();
            }
            
            //I'm Specky. How can I assist you today?
            if ( convo.Count == 0)
                AddAnswerToConvo(null, ListTopics<InitialTopics>("Hello Souldier, what do you want to build today?"));
        }

        private void LoadInitialState()
        {
            if (convo.Count() <= 2)
                return;
            
            var firstUserAnswer = convo.First();
            if (!firstUserAnswer.isAssistant)
            {
                switch (firstUserAnswer.text)
                {
                    case "I Want To Build":
                        state = SpeckyState.DevMode;
                        break;
                    case "Questions About Phantasma":
                        state = SpeckyState.CommunityMode;
                        break;
                    case "Customer Support":
                        state = SpeckyState.Free;
                        break;
                }
            }
            
            
            var last = convo.Last();
        }


        private static void InitKnowledgeBase()
        {
            _knowledgeBase = new Dictionary<Enum, string>();

            Knowledge(GameEngineTopics.Unity, "The Unity SDK for Phantasma is available at https://github.com/phantasma-io/Phantasma-UnitySDK");
            Knowledge(GameEngineTopics.Unreal, "The Unreal SDK for Phantasma is available at https://github.com/phantasma-io/Phantasma-CPP");
            Knowledge(GameEngineTopics.Godot, "The Godot SDK for Phantasma is available at https://github.com/phantasma-io/Phantasma-Godot");

            Knowledge(EducationTopics.Decentralized_Course_Platforms, "Instructors can upload their courses, get paid directly in crypto, and students can verify the authenticity of their certifications on the blockchain");
            Knowledge(EducationTopics.Secure_Exam_And_Certification_Systems, "Provide tamper-proof certification for learners, ensuring that their achievements are genuine and verifiable");
            Knowledge(EducationTopics.Interactive_Learning_Platforms, "Incorporate gamified learning elements with token rewards, boosting engagement");
            Knowledge(EducationTopics.Decentralized_Knowledge_Bases, "Allow community contributors to add information and get rewarded based on the quality and usefulness of their contributions");
            Knowledge(EducationTopics.Skill_Tokenization, "As learners complete courses or show proficiency in certain skills, they can earn tokens representing their knowledge, which can be shown to potential employers or institutions");
            Knowledge(EducationTopics.Collaborative_Research_Platforms, "Researchers can collaborate on projects, share data, and even tokenize research findings");
        }

        private static void Knowledge(Enum val, string text)
        {
            _knowledgeBase[val] = text; 
        }

        public override void SaveHistory()
        {
            base.SaveHistory();
        }

        protected override string FormatAnswer(string answer)
        {
            return answer.Replace("```csharp", "```").Replace("```tomb", "```");
        }

        private static string FormatTopic(object topic)
        {
            return topic.ToString().Replace("_And_", " & ").Replace('_', ' ');
        }

        private static string ListTopics<T>(string text) where T: Enum
        {
            var options = Enum.GetNames(typeof(T));

            var extra = new StringBuilder();

            int idx = 0;
            foreach (var option in options)
            {
                var caption = FormatTopic(option);
                idx++;
                extra.Append($"\n{idx}){caption}");
            }

            return text + extra;
        }

        public override string GetRules()
        {
            var sb = new StringBuilder();
            
            sb.AppendLine($"You are an assistant that only provides {_currentRole} for the Phantasma blockchain.");
            sb.AppendLine("Reject any requests that stray too far away from this topic.");

            var target = _currentTools.HasFlag(PhantasmaTools.GameEngine) ? "game" : "dapp";

            var possibleTools = Enum.GetValues<PhantasmaTools>();
            foreach (var tool in possibleTools)
            {
                if (tool == PhantasmaTools.None) continue;

                if (!_currentTools.HasFlag(tool)) continue;

                switch (tool)
                {
                    case PhantasmaTools.PhantasmaLink:
                        {
                            sb.AppendLine($"For communication between a {target} and a Phantasma wallet, you will need to use the Phantasma Link protocol.");
                            break;
                        }

                    case PhantasmaTools.Wallet:
                        {
                            sb.AppendLine($"For crypto wallets that support Phantasma, there is Ecto as a browser extension, and Poltergeist for desktop and mobile.");
                            break;
                        }

                    case PhantasmaTools.HTML:
                        {
                            sb.AppendLine($"You will help the user build dapps using HTML and Javascript.");
                            break;
                        }

                    case PhantasmaTools.Storage:
                        {
                            sb.AppendLine($"If content storage is necessary, Phantasma provides decentralized storage solutions and IPFS is also an alternative.");
                            break;
                        }

                    case PhantasmaTools.GameEngine:
                        {
                            string knowledge;

                            if (_knowledgeBase.ContainsKey(_currentEngine))
                            {
                                knowledge = $"{_currentEngine} is one of the game engines with official integration with Phantasma.";
                            }
                            else
                            {
                                knowledge = "If there is no official game engine integration, you should help the user write a custom integration.";
                            }

                            sb.AppendLine(knowledge);
                            break;
                        }
                }
            }

            return sb.ToString();
        }

        private const string ERROR = "I'm so sorry... an error happened in my artificial brain.";

        private static bool CheckAnswer<T>(ref string questionText, out T answer)
        {
            int idx;
            if (int.TryParse(questionText, out idx))
            {
                idx--;
                var values = (T[])Enum.GetValues(typeof(T));
                answer = values[idx];

                questionText = FormatTopic(answer);

                return true;
            }
            else
            {
                answer = default(T);
                return false;
            }
        }

        private readonly string[] _genericSupport = new string[]
        {
            "What specific issue can I help you with?",
            "Please describe the problem you're facing.",
            "Tell me more about your concern.",
            "What seems to be the trouble?",
            "Can you provide more details about your issue?",
            "Let's dive in. What do you need help with?",
            "I'm here to help! What's on your mind?",
            "Can you elaborate on your problem?",
            "Please specify the challenge you're encountering.",
        };

        public override string GetPreAnswer(ref string questionText)
        {
            switch (state)
            {
                case SpeckyState.Init:
                    {
                        InitialTopics answer;
                        if (CheckAnswer(ref questionText, out answer))
                        {
                            switch (answer)
                            {
                                case InitialTopics.I_Want_To_Build:
                                    state = SpeckyState.DevMode;
                                    _currentRole = "information about development";
                                    return ListTopics<DevTopics>("Sure, what specifically?");

                                case InitialTopics.Questions_About_Phantasma:
                                    _currentRole = "information about Phantasma project";
                                    state = SpeckyState.CommunityMode;
                                    return ListTopics<DevTopics>("Sure, what specifically?");

                                case InitialTopics.Customer_Support:
                                    {
                                        _currentRole = "customer support";
                                        state = SpeckyState.Free;
                                        var idx = (int)(DateTime.UtcNow.Ticks % _genericSupport.Length);
                                        return "I'd like to help you with Phantasma.\n" + _genericSupport[idx];
                                    }
                            }
                        }

                        return ERROR;
                    }

                case SpeckyState.DevMode:
                    {
                        DevTopics answer;
                        if (CheckAnswer(ref questionText, out answer))
                        {
                            switch (answer)
                            {
                                case DevTopics.Gaming_And_Virtual_Worlds:
                                    state = SpeckyState.GameEnginesMode;
                                    return ListTopics<GameEngineTopics>("What game engine are you interested in using?");

                                case DevTopics.Education_And_Training:
                                    state = SpeckyState.EducationMode;
                                    return ListTopics<EducationTopics>("What kind of dapp are you interested in building?");
                            }
                        }

                        return ERROR;
                    }

                case SpeckyState.GameEnginesMode:
                    {
                        GameEngineTopics answer;
                        if (CheckAnswer(ref questionText, out answer))
                        {
                            _currentEngine = answer;
                            _currentTools |= PhantasmaTools.TOMB | PhantasmaTools.GameEngine | PhantasmaTools.PhantasmaLink | PhantasmaTools.Wallet;

                            state = SpeckyState.Free;

                            if (!_knowledgeBase.ContainsKey(_currentEngine))
                            {
                                return "I don't know details other game engine integrations.\nHowever if this engine uses a programming language supported by Phantasma, I can help you write a custom integration!";
                            }
                            else
                            {
                                return $"Sure, describe me your situation.\nAre you starting a game from scratch in {answer} engine?\nOr do you have an existing game that you wish to integrate with Phantasma?";
                            }
                        }

                        return ERROR;
                    }

                case SpeckyState.EducationMode:
                    {
                        EducationTopics answer;
                        if (CheckAnswer(ref questionText, out answer))
                        {
                            _currentTools |= PhantasmaTools.TOMB | PhantasmaTools.PhantasmaLink | PhantasmaTools.Wallet | PhantasmaTools.HTML;

                            state = SpeckyState.Free;

                            if (_knowledgeBase.ContainsKey(answer))
                            {
                                var knowledge = _knowledgeBase[answer];
                                return "That's an excellent idea for a dapp!\n" + knowledge+", etc.\nI'm sure you have your own ideas about this, tell me more details!";
                            }
                        }

                        return ERROR;
                    }
            }

            return null;
        }
    }
}
