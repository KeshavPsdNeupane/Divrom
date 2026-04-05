using ServiceLocatorPattern;
using UnityEngine;
using UnityEngine.UI;

namespace Kope.SaveSystem {
	public class TempSaveButtons : MonoBehaviour {
		[SerializeField] private Button saveButton;
		[SerializeField] private Button loadButton;
		[SerializeField] private Button commitButton;
		private SceneSaveSystem _SceneSaveSystem;
		private GlobalSaveSystem _GlobalSaveSystem;
		private void Awake() {
			if (!SceneServiceLocator.Instance.TryGetService(out this._SceneSaveSystem)) {
				Debug.LogError("[TempSaveButtons] SceneSaveSystem not found in SceneServiceLocator. Please ensure it is registered in the Bootstrapper.");
			}
			if (!GlobalServiceLocator.Instance.TryGetService(out this._GlobalSaveSystem)) {
				Debug.LogError("[TempSaveButtons] GlobalSaveSystem not found in GlobalServiceLocator. Please ensure it is registered in the Bootstrapper.");
			}
			if (saveButton != null)
				saveButton.onClick.AddListener(OnSave);
			if (loadButton != null)
				loadButton.onClick.AddListener(OnLoadClicked);
			if (commitButton != null)
				commitButton.onClick.AddListener(OnCommit);
		}

		private void OnSave() {
			this._SceneSaveSystem.TriggerSave();
			Debug.Log("Save button clicked. World data saved.");
		}

		private void OnLoadClicked() {
			this._SceneSaveSystem.TriggerLoad();
			Debug.Log("Load button clicked. World data loaded.");
		}
		private void OnCommit() {
			this._GlobalSaveSystem.CommitAllToDisk();
			Debug.Log("Commit button clicked. All buffered scene data committed to disk.");
			// For testing purposes, we can also directly call the save method to see the difference between buffered save and direct save.
			// this._GlobalSaveSystem.SaveWorld();
		}

	}
}
