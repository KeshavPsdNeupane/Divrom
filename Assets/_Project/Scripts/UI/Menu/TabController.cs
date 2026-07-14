using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Kope.Core.LifeTimeManagement;
[System.Serializable]
public class Tab {
	public Image tabImage;
	public GameObject tabPage;
}

public class TabController : InitializableBase {
	[SerializeField] private Tab[] tabs;
	[SerializeField] private Button[] buttons;

	protected override bool OnInit() {
		for (int i = 0; i < buttons.Length; i++) {
			int index = i;
			this.buttons[i].onClick.AddListener(() => ActivateTab(index));
		}
		return true;
	}

	protected override void OnStart() {
		int firstTabIndex = 0;
		ActivateTab(firstTabIndex);
	}

	public void ActivateTab(int tabIndex) {
		for (int i = 0; i < tabs.Length; i++) {
			bool isActive = i == tabIndex;

			if (this.tabs[i].tabImage != null)
				this.tabs[i].tabImage.color = isActive ? Color.white : Color.gray;

			if (this.tabs[i].tabPage != null)
				this.tabs[i].tabPage.SetActive(isActive);
			TMP_Text tmpText = this.tabs[i].tabPage?.GetComponentInChildren<TMP_Text>();
			if (tmpText != null) {
				tmpText.color = isActive ? Color.white : Color.gray;
			}
		}
	}
}
