using System;
using System.Collections.Generic;
using System.Linq;

namespace IndieBuff.Editor
{
    [Serializable]
    public enum ChatMode
    {
        Chat,
        Script,
        Prototype,
    }

    public static class IndieBuff_ChatModeCommands
    {
        public static readonly Dictionary<string, ChatMode> CommandMappings = new Dictionary<string, ChatMode>(StringComparer.OrdinalIgnoreCase)
        {
            { "/chat", ChatMode.Chat },
            { "/script", ChatMode.Script },
            { "/prototype", ChatMode.Prototype },
        };

        public static bool TryGetChatMode(string command, out ChatMode mode)
        {
            return CommandMappings.TryGetValue(command, out mode);
        }

        public static ChatMode GetChatMode(string command)
        {
            return CommandMappings[command];
        }

        public static string GetChatModeCommand(ChatMode mode)
        {
            return CommandMappings.FirstOrDefault(x => x.Value == mode).Key;
        }

        public static string GetPlaceholderString(ChatMode mode)
        {
            return mode switch
            {
                ChatMode.Chat => "Ask IndieBuff any Unity question",
                ChatMode.Script => "Have IndieBuff write code for you",
                ChatMode.Prototype => "Tell IndieBuff what to make",
                _ => throw new ArgumentException($"Unsupported chat mode: {mode}")
            };
        }
    }

}