using System.Collections.Generic;
using ServiceLocatorPattern;
using UnityEngine;

namespace Kope.SaveSystem {
	public class SceneSaveSystem : SceneServiceBase {

		[SerializeField] private int sceneIndex;
		[SerializeField] private string sceneName;

		public int SceneIndex => this.sceneIndex;
		public string SceneName => this.sceneName;
		private GlobalSaveSystem _globalSaveSystem;

		// at max this will be only 1 provider, but we can support more if needed in the future
		// if we are fragmenting the save data into multiple providers, we can have multiple providers in the same scene, 
		// responsible for a different aspect of the save data or different sets of objects. For example, 
		// one provider could be responsible for all the player data, 
		// while another provider could be responsible for all the environment data.
		private readonly Dictionary<SceneDataProviderTypeEnum, ISceneSaveProvider> _saveProviders = new();


		protected override bool OnInitializeService() {
			// base. is not strictly necessary here since the base implementation does nothing, 
			// but it's good practice to call it in case the base class implementation changes in the future.
			base.OnInitializeService();
			if (!GlobalServiceLocator.Instance.TryGetService(out _globalSaveSystem)) {
				Debug.LogError("GlobalSaveSystem missing from GlobalBootStrap!");
				return false;
			}
			return true;
		}

		public void RegisterProvider(ISceneSaveProvider provider) {
			if (!this._saveProviders.ContainsKey(provider.ProviderType)) {
				this._saveProviders.Add(provider.ProviderType, provider);
			}
		}

		public void UnregisterProvider(ISceneSaveProvider provider) {
			this._saveProviders.Remove(provider.ProviderType);
		}

		public void TriggerSave() {
			var data = new Dictionary<SceneDataProviderTypeEnum, SceneSaveDataContainer>();
			foreach (var kvp in this._saveProviders) {
				if (!data.ContainsKey(kvp.Key)) {
					data.Add(kvp.Key, kvp.Value.OnSave());
				}
			}
			var dataAggregate = new SceneSaveDataAggregate(this.SceneIndex, this.SceneName, data);
			this._globalSaveSystem.BufferSceneData(dataAggregate);

			//_globalSaveSystem.CommitAllToDisk();
			Debug.Log($"[SceneSaveSystem] Save triggered for {this._saveProviders.Count} providers.");
		}

		public void TriggerLoad() {

			if (!this._globalSaveSystem.TryGetBufferedSceneData(this.SceneIndex, out var dataAggregate)) {
				Debug.LogWarning($"No buffered data found for scene index {this.SceneIndex}. Load aborted.");
				return;
			}
			foreach (var kvp in dataAggregate.SceneDataByProvider) {
				if (this._saveProviders.TryGetValue(kvp.Key, out var provider)) {
					provider.OnLoad(kvp.Value);
				} else {
					Debug.LogWarning($"No save provider found for provider type {kvp.Key}. Skipping load for this provider.");
				}
			}
			Debug.Log($"[SceneSaveSystem] Load triggered for {this._saveProviders.Count} providers.");
		}
	}
}