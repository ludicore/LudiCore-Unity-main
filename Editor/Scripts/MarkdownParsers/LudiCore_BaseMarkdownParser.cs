using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace IndieBuff.Editor
{
    public abstract class BaseMarkdownParser : IMarkdownParser
    {
        protected bool inCodeBlock;
        protected bool inInlineCodeBlock;
        protected StringBuilder lineBuffer;
        protected StringBuilder fullMessage;
        protected VisualElement messageContainer;
        protected TextField currentMessageLabel;
        protected IndieBuff_SyntaxHighlighter syntaxHighlighter;
        protected string rawCode = "";
        protected bool isFirstChunk;

        protected BaseMarkdownParser(VisualElement responseContainer)
        {
            inCodeBlock = false;
            inInlineCodeBlock = false;
            lineBuffer = new StringBuilder();
            fullMessage = new StringBuilder();
            syntaxHighlighter = new IndieBuff_SyntaxHighlighter();
            isFirstChunk = true;

            messageContainer = responseContainer.Q<VisualElement>("MessageContainer");
            currentMessageLabel = messageContainer.Q<TextField>();
        }

        public void ParseChunk(string chunk)
        {
            fullMessage.Append(chunk);
            foreach (char c in chunk)
            {
                if (c == '\n')
                {
                    if (isFirstChunk)
                    {
                        currentMessageLabel.value = "";
                        IndieBuff_UserInfo.Instance.responseLoadingComplete?.Invoke();
                        messageContainer.parent.style.visibility = Visibility.Visible;
                        isFirstChunk = false;
                    }
                    ProcessLine(lineBuffer.ToString());

                    lineBuffer.Clear();
                }
                else
                {
                    lineBuffer.Append(c);
                }
            }
        }

        public abstract void ParseFullMessage(string message);

        public string GetFullMessage()
        {
            return fullMessage.ToString();
        }

        public abstract void ProcessLine(string line, bool fullMessage = false);

        public virtual void HandleCodeBlockToggle()
        {
            if (inCodeBlock)
            {
                AddCopyButtonToCurrentMessage();
                currentMessageLabel = null;
                rawCode = "";
            }
            else
            {
                currentMessageLabel = CreateNewAIResponseLabel("", "code-block");
            }
            inCodeBlock = !inCodeBlock;
        }

        public virtual void HandleInlineCodeBlockToggle()
        {
            if (inInlineCodeBlock)
            {
                AddCopyButtonToCurrentMessage();
                currentMessageLabel = null;
                rawCode = "";
            }
            else
            {
                currentMessageLabel = CreateNewAIResponseLabel("", "code-block");
            }
            inInlineCodeBlock = !inInlineCodeBlock;
        }

        public virtual void AddCopyButtonToCurrentMessage()
        {
            string codeToCopy = rawCode;
            var copyButton = new Button();
            copyButton.AddToClassList("copy-button");
            copyButton.tooltip = "Copy code";

            var copyButtonIcon = new VisualElement();
            copyButtonIcon.AddToClassList("copy-button-icon");
            copyButton.Add(copyButtonIcon);

            copyButton.clickable.clicked += () => EditorGUIUtility.systemCopyBuffer = codeToCopy;
            currentMessageLabel.Add(copyButton);
        }

        protected string TransformMarkdown(string line)
        {
            line = TransformHeaders(line);
            line = TransformInlineStyles(line);
            return line;
        }
        protected TextField CreateNewAIResponseLabel(string initialText = "", string styleClass = "")
        {
            var label = new TextField
            {
                value = initialText,
                isReadOnly = true,
                multiline = true,
            };
            label.AddToClassList("message-text");
            if (styleClass != "")
            {
                label.AddToClassList(styleClass);
            }

            var textInput = label.Q(className: "unity-text-element");
            if (textInput is TextElement textElement)
            {
                textElement.enableRichText = true;
            }

            messageContainer.Add(label);
            return label;
        }

        public virtual string TransformCodeBlock(string line)
        {
            return syntaxHighlighter.HighlightLine(line) + "\n";
        }

        protected string TransformInlineStyles(string input)
        {

            input = Regex.Replace(input, @"\*\*(.+?)\*\*", "<b>$1</b>");

            input = Regex.Replace(input, @"\*(.+?)\*", "<i>$1</i>");

            input = Regex.Replace(input, "`(.+?)`", "<color=#CDB3FF>$1</color>");

            return input + "\n";
        }

        protected string TransformHeaders(string line)
        {
            string pattern = @"^(#{1,6}) (.+)$";

            var output = Regex.Replace(line, pattern, new MatchEvaluator(ReplaceHeader), RegexOptions.Multiline);
            return output;
        }

        protected string ReplaceHeader(Match match)
        {
            int hashtagCount = match.Groups[1].Value.Length;
            string headerText = match.Groups[2].Value;

            string sizeTag;
            switch (hashtagCount)
            {
                case 1:
                    sizeTag = "<size=14>";
                    break;
                case 2:
                    sizeTag = "<size=16>";
                    break;
                case 3:
                    sizeTag = "<size=18>";
                    break;
                case 4:
                    sizeTag = "<size=20>";
                    break;
                case 5:
                    sizeTag = "<size=22>";
                    break;
                case 6:
                    sizeTag = "<size=24>";
                    break;
                default:
                    sizeTag = "<size=20>";
                    break;
            }

            return $"<b>{sizeTag}{headerText}</b></size>";
        }

        public void TrimMessageEndings()
        {
            foreach (var child in messageContainer.Children())
            {
                if (child is TextField msgLabel)
                {
                    if (msgLabel.value.EndsWith("\n"))
                    {
                        msgLabel.value = msgLabel.value.Substring(0, msgLabel.value.Length - 1);
                    }
                }
            }
        }
    }
}