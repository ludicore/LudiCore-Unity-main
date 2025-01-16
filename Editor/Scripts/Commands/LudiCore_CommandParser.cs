using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Reflection;
using System;
using System.Linq;

namespace IndieBuff.Editor
{
    public class IndieBuff_CommandParser
    {
        public static MethodInfo FindMethod(string methodName)
        {
            return Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => typeof(ICommandManager).IsAssignableFrom(t) && !t.IsInterface)
            .Select(t => t.GetMethod(methodName,
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static))
            .FirstOrDefault(m => m != null);
        }

        public static IndieBuff_CommandData ParseCommandLine(string line)
        {
            var parts = line.Split(new[] { ',' }, 2);
            if (parts.Length != 2)
            {
                Debug.LogError($"Invalid command format: {line}");
                return null;
            }

            string methodName = parts[0].Trim();
            string paramString = parts[1].Trim();

            // Parse parameters by splitting on "][" to separate multiple parameter pairs
            string[] paramPairs = paramString.Trim('[', ']').Split(new[] { "],[" }, StringSplitOptions.RemoveEmptyEntries);


            var result = new Dictionary<string, string>();

            // This regex pattern matches key-value pairs where:
            // - Keys and values are wrapped in quotes
            // - Handles escaped quotes within the values
            // - Accounts for whitespace
            var pattern = @"""((?:[^""\\]|\\.)*)""\s*:\s*""((?:[^""\\]|\\.)*)""";

            var matches = Regex.Matches(paramPairs[0], pattern);

            foreach (Match match in matches)
            {
                if (match.Groups.Count == 3) // Group 0 is full match, 1 is key, 2 is value
                {
                    string key = match.Groups[1].Value;
                    string value = match.Groups[2].Value;

                    // Unescape any escaped characters if needed
                    key = Regex.Unescape(key);
                    value = Regex.Unescape(value);

                    result[key] = value;
                }
            }


            return new IndieBuff_CommandData
            {
                MethodName = methodName,
                Parameters = result
            };
        }


        public static void ExecuteCommand(IndieBuff_CommandData command, bool isPartOfBatch = false)
        {
            try
            {
                MethodInfo methodInfo = FindMethod(command.MethodName);

                if (methodInfo == null)
                {
                    command.ExecutionResult = $"Failed: Method {command.MethodName} not found";
                    Debug.Log(command.ExecutionResult);
                    return;
                }

                // Execute the command
                object result = methodInfo.Invoke(null, new object[] { command.Parameters });
                command.ExecutionResult = result?.ToString() ?? "Command executed successfully";
                Debug.Log(command.ExecutionResult);
            }
            
            catch (Exception e)
            {
                command.ExecutionResult = $"Failed: {e.Message}";
                Debug.LogError($"Error executing command {command.MethodName}: {e}");
            }
        }


        public static void ExecuteAllCommands(List<IndieBuff_CommandData> commands)
        {
            foreach (var command in commands)
            {                
                ExecuteCommand(command, isPartOfBatch: true);
            }
        }
    }

    public class IndieBuff_CommandData
    {
        public string MethodName { get; set; }
        public Dictionary<string, string> Parameters { get; set; }
        public string ExecutionResult { get; set; }

        public string Description { get; set; }

        public override string ToString()
        {
            return $"{MethodName}, {string.Join(", ", Parameters.Select(p => $"[\"{p.Key}\":\"{p.Value}\"]"))}";
        }
    }
}