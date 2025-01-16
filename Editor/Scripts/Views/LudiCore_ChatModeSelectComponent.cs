

using UnityEngine.UIElements;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace IndieBuff.Editor
{
    [System.Serializable]
    public class IndieBuff_ChatModeSelectComponent
    {

        private VisualElement root;
        private VisualElement modeSelectContainer;
        private VisualTreeAsset modeSelectAsset;
        private List<string> availableModes;
        private string currentSelectedMode;
        private Dictionary<string, VisualElement> modeToIconMapping;

        public IndieBuff_ChatModeSelectComponent()
        {
            availableModes = new List<string>();
            modeToIconMapping = new Dictionary<string, VisualElement>();
        }

        public void Initialize()
        {
            if (root != null) return;

            modeSelectAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{IndieBuffConstants.baseAssetPath}/Editor/UXML/IndieBuff_AIModelSelection.uxml");
            if (modeSelectAsset == null)
            {
                Debug.LogError("Failed to load model select asset");
                return;
            }

            root = modeSelectAsset.Instantiate();

            string modeSelectStylePath = $"{IndieBuffConstants.baseAssetPath}/Editor/USS/IndieBuff_AIModelSelection.uss";
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(modeSelectStylePath);

            root.styleSheets.Add(styleSheet);

            root.AddToClassList("ai-model-select-container");
            root.style.position = Position.Absolute;
            root.pickingMode = PickingMode.Position;
            root.style.left = 10;
            root.style.bottom = 35;
            root.style.width = 125;
            modeSelectContainer = root.Q<VisualElement>("AIModelSelectionContainer");

            SetupModeSelectionUI();

            IndieBuff_UserInfo.Instance.onChatModeChanged += OnChatModeChanged;
        }

        private void OnChatModeChanged()
        {
            string newMode = IndieBuff_ChatModeCommands.GetChatModeCommand(IndieBuff_UserInfo.Instance.currentMode);

            if (!string.IsNullOrEmpty(newMode))
            {
                UpdateSelection(newMode);
            }
        }

        private void SetupModeSelectionUI()
        {
            availableModes = IndieBuff_ChatModeCommands.CommandMappings.Keys.ToList();
            string currentPlan = IndieBuff_UserInfo.Instance.currentIndieBuffUser.currentPlan;
            bool isPersonalPlan = currentPlan == "personal";

            foreach (var mode in availableModes)
            {
                Button chatModeSelectionButton = new Button();
                chatModeSelectionButton.AddToClassList("ai-model-selection-button");

                VisualElement chatModeInfo = new VisualElement();
                chatModeInfo.AddToClassList("ai-model-info-container");

                bool isLockedMode = isPersonalPlan && mode != "/chat";

                if (isLockedMode)
                {
                    VisualElement lockIcon = new VisualElement();
                    lockIcon.AddToClassList("ai-model-lock-icon");
                    chatModeInfo.Add(lockIcon);
                    lockIcon.style.display = DisplayStyle.Flex;
                    chatModeSelectionButton.SetEnabled(false);
                    chatModeSelectionButton.tooltip = "Upgrade to unlock chat modes!";
                }


                Label chatModeName = new Label();
                chatModeName.AddToClassList("ai-model-name");
                chatModeName.text = mode;

                chatModeSelectionButton.Add(chatModeInfo);
                chatModeInfo.Add(chatModeName);

                VisualElement chatModeSelectedIcon = new VisualElement();
                chatModeSelectedIcon.AddToClassList("ai-model-selected-icon");
                chatModeSelectionButton.Add(chatModeSelectedIcon);
                chatModeSelectedIcon.style.visibility = Visibility.Hidden;

                modeToIconMapping[mode] = chatModeSelectedIcon;

                if (!isLockedMode)
                {
                    chatModeSelectionButton.clicked += () =>
                    {
                        UpdateSelection(mode);
                        IndieBuff_UserInfo.Instance.currentMode = IndieBuff_ChatModeCommands.CommandMappings[mode];
                    };
                }


                modeSelectContainer.Add(chatModeSelectionButton);

                if (currentSelectedMode == null && IndieBuff_ChatModeCommands.CommandMappings[mode] == IndieBuff_UserInfo.Instance.currentMode)
                {
                    UpdateSelection(mode);
                }
            }
        }

        private void UpdateSelection(string mode)
        {
            currentSelectedMode = mode;

            foreach (var iconPair in modeToIconMapping)
            {
                iconPair.Value.style.visibility = Visibility.Hidden;
                iconPair.Value.parent.RemoveFromClassList("selected");
            }

            if (modeToIconMapping.TryGetValue(mode, out var selectedIcon))
            {
                selectedIcon.style.visibility = Visibility.Visible;
                selectedIcon.parent.AddToClassList("selected");
            }
        }

        public VisualElement GetRoot()
        {
            if (root == null) Initialize();
            return root;
        }
    }
}