using Kope.Core.SaveSystem;
using ServiceLocatorPattern;
using UnityEngine;
using UnityEngine.UI;

namespace Kope.SaveSystem {
	public class TempSaveButtons : MonoBehaviour {
		[SerializeField] private Button saveButton;
		[SerializeField] private Button loadButton;
		private GlobalSaveSystem _globalSaveSystem;
		private void Awake() {
			if (!GlobalServiceLocator.Instance.TryGetService(out this._globalSaveSystem)) {
				Debug.LogError("[TempSaveButtons] GlobalSaveSystem not found in GlobalServiceLocator. Please ensure it is registered in the Bootstrapper.");
			}
			if (saveButton != null)
				saveButton.onClick.AddListener(OnSaveClicked);
			if (loadButton != null)
				loadButton.onClick.AddListener(OnLoadClicked);
		}

		private void OnSaveClicked() {
			this._globalSaveSystem.SaveWorld();
			Debug.Log("Save button clicked. World data saved.");
		}

		private void OnLoadClicked() {
			this._globalSaveSystem.LoadWorld();
			Debug.Log("Load button clicked. World data loaded.");
		}
	}
}
