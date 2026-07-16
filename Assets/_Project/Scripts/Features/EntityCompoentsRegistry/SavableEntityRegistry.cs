using System.Collections.Generic;
using Kope.Core.Identity;
using Kope.Core.Collections.Hashes;
using Kope.SaveSystem;
using Kope.Core.ServiceLocator;
using UnityEngine;

namespace Kope.EntityComponentSystem {

	public class SavableEntityRegistry : SceneServiceBase, ISceneSaveProvider, ISceneSaveProviderNew {

		[SerializeField]
		private SceneDataProviderTypeEnum providerType
		= SceneDataProviderTypeEnum.EntityRegistry;

		// --- OLD SYSTEM ---
		private readonly Dictionary<HashedTag, IEntitySavePacketProvider> _saveAbleEntityes = new();

		// --- NEW SPECIALIZED SYSTEMS ---
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
			// Note: If your SceneSaveSystem only accepts one provider type interface,
			// make sure it supports registering both or maps to the new system accordingly.
			this._sceneSaveSystem.RegisterProvider(this);
			this._sceneSaveSystem.RegisterProviderNew(this);
			return true;
		}

		// --- REGISTRATION HOOKS ---
		public void RegisterEntity(EntitySaveSystem entity) {
			if (entity == null) return;
			var id = entity.UniqueID;
			if (this._saveAbleEntityes.ContainsKey(id)) return;
			this._saveAbleEntityes.Add(id, entity);
		}

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
			if (this._saveAbleEntityes.ContainsKey(entityId)) {
				this._saveAbleEntityes.Remove(entityId);
			}
			// Safely drop from the new tracking tables as well
			this._saveAbleMobEntityes.Remove(entityId);
			this._saveAblePropEntityes.Remove(entityId);
		}

		private int tempCounter = 0;
		void Update() {
			if (this._saveAbleEntityes.Count != tempCounter) {
				tempCounter = this._saveAbleEntityes.Count;
			}
			foreach (var kvp in this._saveAbleEntityes) {
				var entityId = kvp.Key;
				var provider = kvp.Value;
				if (provider == null) {
					Debug.LogWarning($"[EntityRegistrySaveDataManager] Found a null save packet provider for entity ID {entityId}.");
				}
			}
		}

		// ==========================================
		// OLD MONOLITHIC EXECUTION PIPELINE
		// ==========================================
		public SceneSaveDataContainer OnSave() {
			var entitySavePackets = new Dictionary<HashedTag, EntitySavePacket>();
			foreach (var kvp in this._saveAbleEntityes) {
				var provider = kvp.Value;
				var packet = provider.GetEntitySavePacket();
				entitySavePackets[kvp.Key] = packet;
			}
			return new SceneSaveDataContainer(this.ProviderType, entitySavePackets);
		}

		public void OnLoad(SceneSaveDataContainer data) {
			foreach (var kvp in data.EntitySavePackets) {
				var entityId = kvp.Key;
				var packet = kvp.Value;
				if (this._saveAbleEntityes.TryGetValue(entityId, out var provider)) {
					provider.LoadEntitySavePacket(packet);
				} else {
					Debug.LogWarning($"No save packet provider found for entity ID {entityId}. Skipping loading for this entity.");
				}
			}
		}

		// ==========================================
		// NEW SPECIALIZED SPLIT EXECUTION PIPELINE
		// ==========================================
		public SceneSaveDataContainerNew OnSaveNew() {
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

			return new SceneSaveDataContainerNew(this.ProviderType, mobPackets, propPackets);
		}

		public void OnLoadNew(SceneSaveDataContainerNew data) {
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
