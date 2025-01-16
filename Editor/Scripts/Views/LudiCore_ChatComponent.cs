using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Indiebuff.Editor;
using IndieBUff.Editor;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace IndieBuff.Editor
{
    [System.Serializable]
    public class IndieBuff_ChatComponent
    {
        private VisualElement root;
        private IndieBuff_ChatWidgetComponent chatWidgetComponent;

        // response area
        private ScrollView responseArea;

        // top bar buttons
        private Button newChatButton;
        private Button chatHistoryButton;
        private Button profileSettingsButton;
        private Label chatName;

        // chat history panel
        private VisualElement chatHistoryPanel;
        private IndieBuff_ChatHistoryComponent chatHistoryComponent;

        // ai chat settings
        private VisualElement chatSettingsBar;
        private IndieBuff_ChatSettingsComponent chatSettingsComponent;

        // response box
        private VisualTreeAsset AIResponseBoxAsset;

        // ai model selection
        private VisualElement popupContainer;
        private Button aiModelSelectButton;
        private IndieBuff_ModelSelectComponent modelSelectComponent;
        private Label aiModelSelectLabel;

        // chat mode selection
        private Button chatModeSelectButton;
        private IndieBuff_ChatModeSelectComponent chatModeSelectComponent;
        private Label chatModeSelectLabel;

        // pop up
        private VisualElement activePopup = null;
        private VisualElement activeTrigger = null;

        // profile settings
        private IndieBuff_ProfileSettingsComponent profileSettingsComponent;

        // context 
        private Button addContextButton;
        private Button clearContextButton;
        private VisualElement userContextRoot;
        private IndieBuff_AddContextComponent addContextComponent;
        private IndieBuff_SelectedContextViewer selectedContextViewer;

        public event Action OnLogoutSuccess;

        private VisualElement bottombarContainer;

        // loading component
        private ProgressBar loadingComponent;
        private IndieBuff_LoadingBar loadingBar;

        // cancel
        private CancellationTokenSource cts;

        private ResponseHandlerFactory handlerFactory;
        private IResponseHandler currentResponseHandler;

        public IndieBuff_ChatComponent(VisualElement root, VisualTreeAsset aiResponseAsset)
        {
            this.root = root;
            responseArea = root.Q<ScrollView>("ReponseArea");
            chatHistoryPanel = root.Q<VisualElement>("ChatHistoryPanel");
            chatSettingsBar = root.Q<VisualElement>("ChatSettings");
            chatName = root.Q<Label>("ChatName");
            aiModelSelectButton = root.Q<Button>("AIModelSelectButton");
            profileSettingsButton = root.Q<Button>("ProfileButton");
            aiModelSelectLabel = aiModelSelectButton.Q<Label>("AIModelSelectLabel");
            chatModeSelectButton = root.Q<Button>("ChatModeSelectButton");
            chatModeSelectLabel = chatModeSelectButton.Q<Label>("ChatModeSelectLabel");
            userContextRoot = root.Q<VisualElement>("UserContextRoot");
            bottombarContainer = root.Q<VisualElement>("BottomBar");
            loadingComponent = root.Q<ProgressBar>("LoadingBar");

            cts = new CancellationTokenSource();

            handlerFactory = new ResponseHandlerFactory();

            SetupPopupContainer();
            SetupModelSelection();
            SetupChatModeSelection();
            SetupProfileSettings();
            SetupAddContext();

            AIResponseBoxAsset = aiResponseAsset;

            chatHistoryComponent = new IndieBuff_ChatHistoryComponent(chatHistoryPanel, OnChatHistoryClicked);
            chatSettingsComponent = new IndieBuff_ChatSettingsComponent(chatSettingsBar);
            chatWidgetComponent = new IndieBuff_ChatWidgetComponent(root, SendMessageAsync);
            selectedContextViewer = new IndieBuff_SelectedContextViewer(userContextRoot);
            loadingBar = new IndieBuff_LoadingBar(loadingComponent);


            SetupGeometryCallbacks();
            SetupTopBarButtons();

            InitializeConversation();

            aiModelSelectLabel.text = IndieBuff_UserInfo.Instance.selectedModel;
            chatModeSelectLabel.text = IndieBuff_ChatModeCommands.GetChatModeCommand(IndieBuff_UserInfo.Instance.currentMode);

            IndieBuff_ConvoHandler.Instance.onMessagesLoaded += onMessagesLoaded;

            IndieBuff_UserInfo.Instance.onSelectedModelChanged += () =>
            {
                aiModelSelectLabel.text = IndieBuff_UserInfo.Instance.selectedModel;
            };

            IndieBuff_UserInfo.Instance.onChatModeChanged += () =>
            {
                chatModeSelectLabel.text = IndieBuff_ChatModeCommands.GetChatModeCommand(IndieBuff_UserInfo.Instance.currentMode);
            };

            IndieBuff_UserInfo.Instance.responseLoadingComplete += () =>
            {
                loadingBar.StopLoading();
            };


            IndieBuff_ConvoHandler.Instance.onConvoTitleChanged += () =>
            {
                chatName.text = IndieBuff_ConvoHandler.Instance.currentConvoTitle;
            };
        }

        private void SetupPopupContainer()
        {
            popupContainer = root.Q<VisualElement>("PopupContainer");

            popupContainer.style.position = Position.Absolute;
            popupContainer.style.left = 0;
            popupContainer.style.top = 0;
            popupContainer.style.right = 0;
            popupContainer.style.bottom = 0;
            popupContainer.pickingMode = PickingMode.Ignore;

            root.RegisterCallback<PointerDownEvent>(OnRootPointerDown, TrickleDown.TrickleDown);
        }

        private void SetupChatModeSelection()
        {
            chatModeSelectComponent = new IndieBuff_ChatModeSelectComponent();
            chatModeSelectButton.clicked += () =>
            {
                ShowPopup(chatModeSelectComponent.GetRoot(), chatModeSelectButton);
            };

        }

        private void SetupModelSelection()
        {
            modelSelectComponent = new IndieBuff_ModelSelectComponent();
            aiModelSelectButton.clicked += () =>
            {
                ShowPopup(modelSelectComponent.GetRoot(), aiModelSelectButton);
            };
        }

        private void SetupProfileSettings()
        {
            profileSettingsComponent = new IndieBuff_ProfileSettingsComponent(OnLogoutClicked);
            profileSettingsButton.clicked += () =>
            {
                ShowPopup(profileSettingsComponent.GetRoot(), profileSettingsButton);
            };
        }

        private void SetupAddContext()
        {
            addContextButton = root.Q<Button>("AddContextButton");
            clearContextButton = root.Q<Button>("ClearContextButton");
            
            // Initial state check
            UpdateClearButtonVisibility();
            
            // Subscribe to context updates
            IndieBuff_UserSelectedContext.Instance.onUserSelectedContextUpdated += UpdateClearButtonVisibility;

            addContextComponent = new IndieBuff_AddContextComponent();

            addContextButton.clicked += () =>
            {
                ShowPopup(addContextComponent.GetRoot(), addContextButton, true);
            };

            clearContextButton.clicked += () =>
            {
                addContextComponent.ClearContextItems();
            };
        }

        private void UpdateClearButtonVisibility()
        {
            var context = IndieBuff_UserSelectedContext.Instance;
            bool hasContent = context.UserContextObjects.Count > 0 || context.ConsoleLogs.Count > 0;
            clearContextButton.style.visibility = hasContent ? Visibility.Visible : Visibility.Hidden;
        }

        private void ShowPopup(VisualElement popup, VisualElement trigger, bool followTrigger = false)
        {
            if (activePopup == popup && activeTrigger == trigger)
            {
                HidePopup();
                return;
            }

            popupContainer.Clear();
            activePopup = popup;
            activeTrigger = trigger;
            popupContainer.Add(popup);

            if (followTrigger)
            {
                popup.style.position = Position.Absolute;
                popup.style.bottom = root.worldBound.height - trigger.worldBound.y + 35;
            }
        }

        private void OnRootPointerDown(PointerDownEvent evt)
        {
            if (activePopup != null)
            {
                if (!activePopup.worldBound.Contains(evt.position) &&
                    (activeTrigger == null || !activeTrigger.worldBound.Contains(evt.position)))
                {
                    HidePopup();
                }
            }
        }

        private void HidePopup()
        {
            popupContainer.Clear();
            activePopup = null;
            activeTrigger = null;
        }

        private void SetupGeometryCallbacks()
        {
            bottombarContainer.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                var newHeight = bottombarContainer.resolvedStyle.height;
                chatHistoryPanel.style.bottom = newHeight;
                addContextComponent.GetRoot().style.bottom = root.worldBound.height - addContextButton.worldBound.y + 35;
            });
        }

        public void Cleanup()
        {
            IndieBuff_ConvoHandler.Instance.onMessagesLoaded -= onMessagesLoaded;
            chatHistoryComponent.Cleanup();
            //chatWidgetComponent.Cleanup();
        }

        private void InitializeConversation()
        {
            _ = InitializeConversationAsync();
        }

        private async Task InitializeConversationAsync()
        {

            List<IndieBuff_MessageData> messages = IndieBuff_ConvoHandler.Instance.currentMessages;
            foreach (var message in messages)
            {
                if (message.Role == "user")
                {
                    var userMessage = message.Content;
                    AddUserMessageToResponseArea($"<b><b>You:</b></b>\n{userMessage}");
                }
                else if (message.Role == "assistant")
                {
                    var aiMessage = message.Content;

                    var responseContainer = CreateAIChatResponseBox("");
                    responseArea.Add(responseContainer);

                    IResponseHandler responseHandler = handlerFactory.CreateHandler(message.ChatMode, responseContainer, IndieBuff_UserInfo.ShouldUseDiffFormat(message.AiModel));
                    responseHandler.HandleFullResponse(aiMessage);
                }
            }

            chatName.text = IndieBuff_ConvoHandler.Instance.currentConvoTitle;
            await Task.Delay(100);
            ScrollToBottom();
        }

        private async Task HandleAIResponse(string userMessage)
        {
            loadingBar.StartLoading();
            currentResponseHandler?.Cleanup();

            var responseContainer = CreateAIChatResponseBox("");
            responseArea.Add(responseContainer);
            responseContainer.style.visibility = Visibility.Hidden;

            await Task.Delay(50);
            responseArea.ScrollTo(responseContainer);



            currentResponseHandler = handlerFactory.CreateHandler(IndieBuff_UserInfo.Instance.currentMode, responseContainer, IndieBuff_UserInfo.ShouldUseDiffFormat(IndieBuff_UserInfo.Instance.selectedModel));

            cts = new CancellationTokenSource();

            await currentResponseHandler.HandleResponse(userMessage, responseContainer, cts.Token);
        }

        private async void onMessagesLoaded()
        {
            responseArea.Clear();
            await InitializeConversationAsync();
        }

        private void OnNewChatClicked()
        {
            IndieBuff_ConvoHandler.Instance.ClearConversation();
            IndieBuff_UserInfo.Instance.NewConversation();
            responseArea.Clear();
        }

        private void ScrollToBottom()
        {
            float contentHeight = responseArea.contentContainer.layout.height;
            float viewportHeight = responseArea.layout.height;
            float maxScroll = Mathf.Max(0, contentHeight - viewportHeight);

            responseArea.scrollOffset = new Vector2(0, maxScroll);
            responseArea.MarkDirtyRepaint();
        }

        private void TrimMessageEndings(VisualElement msgContainer)
        {
            foreach (var child in msgContainer.Children())
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

        private void SetupTopBarButtons()
        {
            newChatButton = root.Q<Button>("NewChatButton");
            chatHistoryButton = root.Q<Button>("ChatHistoryButton");
            profileSettingsButton = root.Q<Button>("ProfileButton");

            newChatButton.clicked += OnNewChatClicked;
            chatHistoryButton.clicked += OnChatHistoryClicked;
        }

        private void OnChatHistoryClicked()
        {
            float panelWidth = chatHistoryPanel.resolvedStyle.width;

            if (chatHistoryPanel.style.translate == new Translate(0, 0, 0))
            {
                chatHistoryPanel.style.translate = new Translate(-panelWidth, 0, 0);
            }
            else
            {
                chatHistoryPanel.style.translate = new Translate(0, 0, 0);
            }
        }

        private async Task SendMessageAsync(VisualElement chatWidget)
        {
            TextField chatInputArea = chatWidget.Q<TextField>("ChatInputArea");
            Button sendChatButton = chatWidget.Q<Button>("SendChatButton");

            if (IndieBuff_UserInfo.Instance.isStreamingMessage)
            {
                cts.Cancel();
                responseArea.RemoveAt(responseArea.childCount - 1);
                sendChatButton.Q<VisualElement>("StopChatIcon").style.display = DisplayStyle.None;
                sendChatButton.Q<VisualElement>("SendChatIcon").style.display = DisplayStyle.Flex;

                return;
            }

            string userMessage = chatInputArea.text.Trim();
            if (string.IsNullOrEmpty(userMessage))
            {
                return;
            }
            IndieBuff_UserInfo.Instance.isStreamingMessage = true;
            sendChatButton.Q<VisualElement>("SendChatIcon").style.display = DisplayStyle.None;
            sendChatButton.Q<VisualElement>("StopChatIcon").style.display = DisplayStyle.Flex;

            AddUserMessageToResponseArea($"<b><b>You:</b></b>\n{userMessage}");

            chatInputArea.value = string.Empty;
            await HandleAIResponse(userMessage);

            IndieBuff_UserInfo.Instance.isStreamingMessage = false;
            sendChatButton.Q<VisualElement>("StopChatIcon").style.display = DisplayStyle.None;
            sendChatButton.Q<VisualElement>("SendChatIcon").style.display = DisplayStyle.Flex;

        }

        private void AddUserMessageToResponseArea(string message)
        {
            var messageContainer = new VisualElement();
            messageContainer.AddToClassList("chat-message");

            var messageLabel = new TextField
            {
                value = message,
                isReadOnly = true,
                multiline = true,
            };

            var textInput = messageLabel.Q(className: "unity-text-element");
            if (textInput is TextElement textElement)
            {
                textElement.enableRichText = true;
            }

            messageLabel.AddToClassList("message-text");
            messageLabel.pickingMode = PickingMode.Position;

            messageContainer.Add(messageLabel);
            responseArea.Add(messageContainer);
        }

        private VisualElement CreateAIChatResponseBox(string initialText = "Loading...")
        {
            var aiMessageContainer = AIResponseBoxAsset.CloneTree();

            string responseBoxStylePath = $"{IndieBuffConstants.baseAssetPath}/Editor/USS/IndieBuff_AIResponse.uss";
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(responseBoxStylePath);

            aiMessageContainer.styleSheets.Add(styleSheet);
            var messageContainer = aiMessageContainer.Q<VisualElement>("MessageContainer");
            var messageLabel = new TextField
            {
                value = initialText,
                isReadOnly = true,
                multiline = true,
            };
            messageLabel.AddToClassList("message-text");
            messageContainer.Add(messageLabel);

            var textInput = messageLabel.Q(className: "unity-text-element");
            if (textInput is TextElement textElement)
            {
                textElement.enableRichText = true;
            }

            return aiMessageContainer;
        }

        private async void OnLogoutClicked()
        {
            bool userConfirmed = EditorUtility.DisplayDialog(
                "Confirm Logout",
                "Are you sure you want to logout?",
                "Yes",
                "Cancel"
            );

            if (userConfirmed)
            {
                await IndieBuff_ApiClient.Instance.LogoutUser();

                OnLogoutSuccess?.Invoke();
            }
        }

    }
}