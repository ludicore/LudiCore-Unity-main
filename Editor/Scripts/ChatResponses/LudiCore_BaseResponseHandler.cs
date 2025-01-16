using System;
using System.Threading;
using System.Threading.Tasks;
using IndieBUff.Editor;
using UnityEngine;
using UnityEngine.UIElements;

namespace IndieBuff.Editor
{
    public abstract class BaseResponseHandler : IResponseHandler
    {
        protected readonly IMarkdownParser parser;
        protected BaseResponseHandler(IMarkdownParser parser)
        {
            this.parser = parser;
        }

        public async Task HandleResponse(string userMessage, VisualElement responseContainer, CancellationToken token)
        {
            var messageContainer = responseContainer.Q<VisualElement>("MessageContainer");
            var messageLabel = messageContainer.Q<TextField>();
            try
            {

                await IndieBuff_ApiClient.Instance.StreamChatMessageAsync(userMessage, (chunk) =>
                {
                    parser.ParseChunk(chunk);
                }, token);

                await OnStreamComplete();

                if (token.IsCancellationRequested)
                {
                    return;
                }

                await HandleResponseMetadata(userMessage, parser);

                await OnProcessingComplete();

                parser.TrimMessageEndings();


            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                HandleError(responseContainer);
            }
        }

        public void HandleFullResponse(string aiMessage)
        {
            parser.ParseFullMessage(aiMessage);
            parser.TrimMessageEndings();
        }

        protected virtual Task OnStreamComplete() => Task.CompletedTask;
        protected virtual Task OnProcessingComplete() => Task.CompletedTask;

        public void HandleError(VisualElement responseContainer)
        {
            var messageContainer = responseContainer.Q<VisualElement>("MessageContainer");
            var messageLabel = messageContainer.Q<TextField>();

            responseContainer.style.visibility = Visibility.Visible;
            messageLabel.value = "An error has occured. Please try again.";
            IndieBuff_UserInfo.Instance.responseLoadingComplete?.Invoke();
        }

        public virtual void Cleanup()
        {

        }

        private async Task HandleChatDatabase(string userMessage, string aiMessage, string summaryMessage = "")
        {
            IndieBuff_UserInfo.Instance.lastUsedMode = IndieBuff_UserInfo.Instance.currentMode;
            IndieBuff_UserInfo.Instance.lastUsedModel = IndieBuff_UserInfo.Instance.selectedModel;
            if (!string.IsNullOrWhiteSpace(summaryMessage))
            {
                await IndieBuff_ConvoHandler.Instance.AddMessage("summary", summaryMessage, IndieBuff_UserInfo.Instance.lastUsedMode, IndieBuff_UserInfo.Instance.lastUsedModel);
            }
            await IndieBuff_ConvoHandler.Instance.AddMessage("user", userMessage, IndieBuff_UserInfo.Instance.lastUsedMode, IndieBuff_UserInfo.Instance.lastUsedModel);
            await IndieBuff_ConvoHandler.Instance.AddMessage("assistant", aiMessage, IndieBuff_UserInfo.Instance.lastUsedMode, IndieBuff_UserInfo.Instance.lastUsedModel);

            await IndieBuff_ConvoHandler.Instance.RefreshConvoList();
        }

        protected async Task HandleResponseMetadata(string userMessage, IMarkdownParser parser)
        {
            int splitIndex = parser.GetFullMessage().LastIndexOf('\n');
            string aiMessage;
            string summaryMessage;

            if (splitIndex != -1)
            {
                aiMessage = parser.GetFullMessage().Substring(0, splitIndex);
                string jsonInput = parser.GetFullMessage().Substring(splitIndex + 1).Trim();
                var parsedJson = JsonUtility.FromJson<IndieBuff_SummaryResponse>(jsonInput);
                summaryMessage = parsedJson.content;
            }
            else
            {
                aiMessage = parser.GetFullMessage();
                summaryMessage = "";
            }

            await HandleChatDatabase(userMessage, aiMessage, summaryMessage);
        }

    }
}