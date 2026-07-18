using System.Collections.Generic;
using Kope.Core.Identity;
using Kope.Core.Collections.Hashes;
using Kope.SaveSystem;
using Kope.Core.ServiceLocator;
using UnityEngine;

namespace Kope.EntityComponentSystem {

	public class SavableEntityRegistry : SceneServiceBase, ISceneSaveProvider {

		[SerializeField]
		private SceneDataProviderTypeEnum providerType = SceneDataProviderTypeEnum.EntityRegistry;

		// --- SPECIALIZED SYSTEMS ---
		private readonly Dictionary<HashedTag, IMobEntitySavePacketProvider> _saveAbleMobEntityes = new();
		private readonly Dictionary<HashedTag, IPropEntitySavePacketProvider> _saveAblePropEntityes = new();

		private SceneSaveSystem _sceneSaveSystem;
		public SceneDataProviderTypeEnum ProviderType => this.providerType;

		protected override bool OnInitializeService() {
			Awake();
			if (!SceneServiceLocator.Instance.TryGetService(out this._sceneSaveSystem)) {
				Debug.LogError("SceneSaveSystem is not registered in SceneServiceLocator. Please check your SceneBootStrap!");
				return false;
			}

			this._sceneSaveSystem.RegisterProvider(this);
			return true;
		}

		// --- REGISTRATION HOOKS ---
		public void RegisterMobEntity(MobEntitySaveSystem entity) {
			if (entity == null) return;
			var id = entity.UniqueID;
			if (this._saveAbleMobEntityes.ContainsKey(id)) return;
			this._saveAbleMobEntityes.Add(id, entity);
		}

		public void RegisterPropEntity(PropEntitySaveSystem entity) {
			if (entity == null) return;
			var id = entity.UniqueID;
			if (this._saveAblePropEntityes.ContainsKey(id)) return;
			this._saveAblePropEntityes.Add(id, entity);
		}

		public void UnregisterEntity(HashedTag entityId) {
			// Safely drop from the tracked runtime tables
			this._saveAbleMobEntityes.Remove(entityId);
			this._saveAblePropEntityes.Remove(entityId);
		}

		private int tempCounter = 0;
		void Update() {
			int currentCount = this._saveAbleMobEntityes.Count + this._saveAblePropEntityes.Count;
			if (currentCount != tempCounter) {
				tempCounter = currentCount;
			}

			foreach (var kvp in this._saveAbleMobEntityes) {
				if (kvp.Value == null) {
					Debug.LogWarning($"[SavableEntityRegistry] Found a null save packet provider for Mob ID {kvp.Key}.");
				}
			}

			foreach (var kvp in this._saveAblePropEntityes) {
				if (kvp.Value == null) {
					Debug.LogWarning($"[SavableEntityRegistry] Found a null save packet provider for Prop ID {kvp.Key}.");
				}
			}
		}

		// ==========================================
		// SPECIALIZED SPLIT EXECUTION PIPELINE
		// ==========================================
		public SceneSaveDataContainer OnSave() {
			var mobPackets = new Dictionary<HashedTag, MobEntitySavePacket>();
			foreach (var kvp in this._saveAbleMobEntityes) {
				if (kvp.Value != null) {
					mobPackets[kvp.Key] = kvp.Value.GetEntitySavePacket();
				}
			}

			var propPackets = new Dictionary<HashedTag, PropEntitySavePacket>();
			foreach (var kvp in this._saveAblePropEntityes) {
				if (kvp.Value != null) {
					propPackets[kvp.Key] = kvp.Value.GetEntitySavePacket();
				}
			}

			return new SceneSaveDataContainer(this.ProviderType, mobPackets, propPackets);
		}

		public void OnLoad(SceneSaveDataContainer data) {
			if (data.MobEntitySavePackets != null) {
				foreach (var kvp in data.MobEntitySavePackets) {
					if (this._saveAbleMobEntityes.TryGetValue(kvp.Key, out var provider)) {
						provider.LoadEntitySavePacket(kvp.Value);
					} else {
						Debug.LogWarning($"[SavableEntityRegistry] No active Mob found for saved ID: {kvp.Key}. Skipping load.");
					}
				}
			}

			if (data.PropEntitySavePackets != null) {
				foreach (var kvp in data.PropEntitySavePackets) {
					if (this._saveAblePropEntityes.TryGetValue(kvp.Key, out var provider)) {
						provider.LoadEntitySavePacket(kvp.Value);
					} else {
						Debug.LogWarning($"[SavableEntityRegistry] No active Prop found for saved ID: {kvp.Key}. Skipping load.");
					}
				}
			}
		}
	}
}