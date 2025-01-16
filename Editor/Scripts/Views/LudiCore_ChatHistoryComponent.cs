using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace IndieBuff.Editor
{
    [System.Serializable]
    public class IndieBuff_ChatHistoryComponent
    {
        private VisualElement chatPanel;
        private ScrollView historyScrollView;
        private float panelWidth;
        private Action closePanelAction;

        public IndieBuff_ChatHistoryComponent(VisualElement chatPanel, Action closePanelAction)
        {
            this.chatPanel = chatPanel;
            this.closePanelAction = closePanelAction;
            panelWidth = chatPanel.resolvedStyle.width;
            historyScrollView = chatPanel.Q<ScrollView>("ChatHistoryScrollView");

            IndieBuff_ConvoHandler.Instance.onConversationsLoaded += () => SetUpChatHistory();
            SetUpChatHistory();
        }

        public void Cleanup()
        {
            IndieBuff_ConvoHandler.Instance.onConversationsLoaded -= () => SetUpChatHistory();
        }

        public void SetUpChatHistory()
        {
            historyScrollView.Clear();
            var convos = IndieBuff_ConvoHandler.Instance.conversations;

            foreach (var convo in convos)
            {
                var chatHistoryItem = new VisualElement();
                chatHistoryItem.AddToClassList("chat-history-list-item");
                chatHistoryItem.tooltip = convo.Title;

                var chatHistoryItemButton = new Button();
                chatHistoryItemButton.AddToClassList("chat-history-item-button");
                chatHistoryItemButton.text = convo.Title;
                chatHistoryItemButton.enableRichText = false;

                var chatHistoryItemDeleteButton = new Button();
                chatHistoryItemDeleteButton.AddToClassList("chat-history-item-delete-button");
                chatHistoryItemDeleteButton.text = "X";

                chatHistoryItem.Add(chatHistoryItemButton);
                chatHistoryItem.Add(chatHistoryItemDeleteButton);

                chatHistoryItemButton.clicked += async () =>
                {
                    if (IndieBuff_ConvoHandler.Instance.currentConvoId != convo.ConversationId)
                    {
                        IndieBuff_ConvoHandler.Instance.currentConvoId = convo.ConversationId;
                        IndieBuff_ConvoHandler.Instance.currentConvoTitle = convo.Title;
                        await IndieBuff_ConvoHandler.Instance.RefreshCurrentConversation();
                    }

                    closePanelAction?.Invoke();
                };

                chatHistoryItemDeleteButton.clicked += async () =>
                {
                    try
                    {
                        chatHistoryItemDeleteButton.SetEnabled(false);

                        await IndieBuff_ConvoHandler.Instance.DeleteConversation(convo.ConversationId);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error deleting conversation: {ex.Message}");
                        chatHistoryItemDeleteButton.SetEnabled(true);
                    }

                };

                historyScrollView.Add(chatHistoryItem);
            }

            chatPanel.MarkDirtyRepaint();
        }
    }
}