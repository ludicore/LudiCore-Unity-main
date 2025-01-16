using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine.UIElements;

namespace IndieBuff.Editor
{
    public class ChatParser : BaseMarkdownParser
    {
        private int chunkSize = 20;
        private int typingDelayMs = 10;

        public ChatParser(VisualElement responseContainer)
             : base(responseContainer) { }

        public override void ParseFullMessage(string message)
        {
            fullMessage.Append(message);
            var lines = message.Split(new[] { '\n' }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                ProcessLine(line, true);
            }
        }

        public override void ProcessLine(string line, bool fullMessage = false)
        {
            if (line.StartsWith("```"))
            {
                HandleCodeBlockToggle();
                return;
            }
            else if (!inCodeBlock && (line.Equals("`csharp") || line.Equals("`")))
            {
                HandleInlineCodeBlockToggle();
                return;
            }

            currentMessageLabel ??= CreateNewAIResponseLabel("",
                    inCodeBlock || inInlineCodeBlock ? "code-block" : "message-text");

            string processedLine = inCodeBlock || inInlineCodeBlock ? TransformCodeBlock(line) : TransformMarkdown(line);


            if (inCodeBlock || inInlineCodeBlock)
            {
                rawCode += line + "\n";
                currentMessageLabel.value += processedLine;
                return;
            }

            if (!fullMessage)
            {
                //await TypeTextAnimation(processedLine);
                currentMessageLabel.value += processedLine;
            }
            else
            {
                currentMessageLabel.value += processedLine;
            }

        }

        private async Task TypeTextAnimation(string text)
        {
            TextField targetLabel = currentMessageLabel;
            string originalContent = targetLabel.value;

            for (int i = 0; i < text.Length; i += chunkSize)
            {
                int charactersToTake = Math.Min(chunkSize, text.Length - i);
                targetLabel.value = originalContent + text.Substring(0, i + charactersToTake);
                await Task.Delay(typingDelayMs);
            }

        }

    }
}