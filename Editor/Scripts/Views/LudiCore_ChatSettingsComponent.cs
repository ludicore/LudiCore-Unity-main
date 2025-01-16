
using UnityEngine;
using UnityEngine.UIElements;

namespace IndieBuff.Editor
{
    [System.Serializable]
    public class IndieBuff_ChatSettingsComponent
    {

        private VisualElement chatSettingsBar;
        private Button upgradeButton;
        private VisualElement upgradeContainer;

        public IndieBuff_ChatSettingsComponent(VisualElement chatSettingsBar)
        {
            this.chatSettingsBar = chatSettingsBar;

            upgradeContainer = chatSettingsBar.Q<VisualElement>("UpgradeLabelContainer");
            upgradeButton = chatSettingsBar.Q<Button>("UpgradeButton");

            upgradeButton.clicked += () =>
            {
                Application.OpenURL(IndieBuff_EndpointData.GetFrontendBaseUrl() + "/pricing");
            };

            if (IndieBuff_UserInfo.Instance.currentUser.currentPlan == "personal")
            {
                upgradeContainer.style.display = DisplayStyle.Flex;
            }
            else
            {
                upgradeContainer.style.display = DisplayStyle.None;
            }


        }

    }
}