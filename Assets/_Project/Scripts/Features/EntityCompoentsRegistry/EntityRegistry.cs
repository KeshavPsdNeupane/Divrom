using System.Collections.Generic;
using Kope.Core.Identity;
using Kope.Core.Collections.Hashes;
using Kope.SaveSystem;
using Kope.Core.ServiceLocator;
using UnityEngine;

namespace Kope.EntityComponentSystem {

	/// <summary>
	/// Central scene service managing entity references and save state coordination within the current scene scope.
	/// </summary>
	/*
     * ====================================================================================================
     * ARCHITECTURAL RATIONALE FOR CONSOLIDATED SCENE SERVICE:
     * ====================================================================================================
     * Combining Live Runtime Tracking and Save Provider Registration into a single SceneServiceBase
     * prevents service class proliferation and provides four core architectural benefits:
     *
     * 1. Service Locator & Bootstrap Efficiency:
     *    Splitting entity tracking and saving into two separate services (e.g., EntityTrackerService and
     *    EntitySaveRegistryService) creates unnecessary lifecycle overhead in SceneBootstrap. Consolidating
     *    them reduces SceneServiceLocator lookup calls, simplifies scene initialization, and minimizes 
     *    service map clutter.
     *
     * 2. Single Source of Truth for HashedTag Identity:
     *    Both live instances and save providers share the exact same underlying key domain: the entity's 
     *    scene-unique HashedTag. Hosting both lookups within a single authority guarantees a unified entity 
     *    boundary across the scene.
     *
     * 3. High Cohesion without Sacrificing Separation of Concerns:
     *    Merging the service class does NOT leak save concepts into live entities. EntityInstance registers 
     *    for gameplay lookups with zero awareness of saving. EntitySaveSystemBase registers save providers
     *    independently. Internal dictionary isolation (_mobEntities vs _saveableMobEntities) maintains clean
     *    decoupling at the data layer without paying the architectural tax of multiple Unity services.
     *
     * 4. Zero Allocation Runtime Execution:
     *    Validation is driven on-demand during OnSave()/OnLoad() steps rather than polling dictionaries 
     *    in FixedUpdate(), keeping normal runtime gameplay strictly zero-allocation.
     * ====================================================================================================
     */
	public class EntityRegistry : SceneServiceBase, ISceneSaveProvider {

		[SerializeField]
		private SceneDataProviderTypeEnum providerType = SceneDataProviderTypeEnum.EntityRegistry;

		// ==========================================
		// RUNTIME ENTITY STORAGE
		// ==========================================

		/// <summary>Active runtime mobs indexed by their scene-unique HashedTag.</summary>
		private readonly Dictionary<HashedTag, MobInstance> _mobEntities = new();

		/// <summary>Active runtime props indexed by their scene-unique HashedTag.</summary>
		private readonly Dictionary<HashedTag, PropInstance> _propEntities = new();

		/// <summary>
		/// Cached reference to the local scene's player entity instance.
		/// Scoped strictly to this scene's registry instance.
		/// </summary>
		private MobInstance _playerEntityInstance;
		public MobInstance PlayerEntityInstance => this._playerEntityInstance;

		// ==========================================
		// SAVE PROVIDER STORAGE
		// ==========================================

		/// <summary>Active mob save packet providers registered independently by save components.</summary>
		private readonly Dictionary<HashedTag, IMobEntitySavePacketProvider> _saveableMobEntities = new();

		/// <summary>Active prop save packet providers registered independently by save components.</summary>
		private readonly Dictionary<HashedTag, IPropEntitySavePacketProvider> _saveablePropEntities = new();

		private SceneSaveSystem _sceneSaveSystem;
		public SceneDataProviderTypeEnum ProviderType => this.providerType;

		#region Service Lifecycle

		protected override bool OnInitializeService() {
			Awake();
			if (!SceneServiceLocator.Instance.TryGetService(out this._sceneSaveSystem)) {
				Debug.LogError("[EntityRegistry] SceneSaveSystem is not registered in SceneServiceLocator. Check SceneBootstrap initialization order!");
				return false;
			}

			this._sceneSaveSystem.RegisterProvider(this);
			return true;
		}

		#endregion

		#region Live Entity Registration

		/// <summary>
		/// Registers a live runtime entity instance into the registry.
		/// Called automatically by <see cref="EntityInstance"/> during its lifecycle setup.
		/// </summary>
		public void RegisterEntity(EntityInstance entity) {
			if (entity == null) return;

			switch (entity) {
				case MobInstance mob:
					RegisterMobInstance(mob);
					break;
				case PropInstance prop:
					RegisterPropInstance(prop);
					break;
				default:
					Debug.LogWarning($"[EntityRegistry] Unknown EntityInstance subtype '{entity.GetType().Name}' on registration. Entity skipped.");
					break;
			}
		}

		private void RegisterMobInstance(MobInstance entity) {
			var id = entity.UniqueTag;
			if (!this._mobEntities.TryAdd(id, entity)) return;

			if (!entity.IsPlayer) return;

			if (this._playerEntityInstance != null && this._playerEntityInstance != entity) {
				Debug.LogWarning($"[EntityRegistry] Player entity '{this._playerEntityInstance.name}' is already registered. '{entity.name}' has overridden it.");
			}
			this._playerEntityInstance = entity;
			Debug.Log($"[EntityRegistry] Player entity '{entity.name}' registered successfully.");
		}

		private void RegisterPropInstance(PropInstance entity) {
			var id = entity.UniqueTag;
			this._propEntities.TryAdd(id, entity);
		}

		/// <summary>
		/// Removes a live runtime entity from the registry upon destruction or pooling release.
		/// </summary>
		public void UnregisterEntity(EntityInstance entity) {
			if (entity == null) return;
			var id = entity.UniqueTag;

			switch (entity) {
				case MobInstance mob:
					this._mobEntities.Remove(id);
					if (this._playerEntityInstance == mob) this._playerEntityInstance = null;
					break;
				case PropInstance:
					this._propEntities.Remove(id);
					break;
			}
		}

		#endregion

		#region Save System Registration

		/// <summary>
		/// Registers a mob save packet provider.
		/// Decoupled from <see cref="MobInstance"/>; called independently by <see cref="MobEntitySaveSystem"/>.
		/// </summary>
		public void RegisterSavableEntity(MobEntitySaveSystem entity) {
			if (entity == null) return;
			var id = entity.UniqueID;
			if (this._saveableMobEntities.ContainsKey(id)) return;
			this._saveableMobEntities.Add(id, entity);
		}

		/// <summary>
		/// Registers a prop save packet provider.
		/// Decoupled from <see cref="PropInstance"/>; called independently by <see cref="PropEntitySaveSystem"/>.
		/// </summary>
		public void RegisterSavableEntity(PropEntitySaveSystem entity) {
			if (entity == null) return;
			var id = entity.UniqueID;
			if (this._saveablePropEntities.ContainsKey(id)) return;
			this._saveablePropEntities.Add(id, entity);
		}

		/// <summary>
		/// Drops save providers from the active save tracking maps by ID.
		/// </summary>
		public void UnregisterEntity(HashedTag entityId) {
			this._saveableMobEntities.Remove(entityId);
			this._saveablePropEntities.Remove(entityId);
		}

		#endregion

		#region ISceneSaveProvider Implementation

		public SceneSaveDataContainer OnSave() {
			var mobPackets = new Dictionary<HashedTag, MobEntitySavePacket>();
			foreach (var (key, provider) in this._saveableMobEntities) {
				if (provider != null) {
					mobPackets[key] = provider.GetEntitySavePacket();
				} else {
					Debug.LogWarning($"[EntityRegistry] Null Mob save packet provider encountered for ID: {key}. Skipping.");
				}
			}

			var propPackets = new Dictionary<HashedTag, PropEntitySavePacket>();
			foreach (var (key, provider) in this._saveablePropEntities) {
				if (provider != null) {
					propPackets[key] = provider.GetEntitySavePacket();
				} else {
					Debug.LogWarning($"[EntityRegistry] Null Prop save packet provider encountered for ID: {key}. Skipping.");
				}
			}

			return new SceneSaveDataContainer(this.ProviderType, mobPackets, propPackets);
		}

		public void OnLoad(SceneSaveDataContainer data) {
			if (data.MobEntitySavePackets != null) {
				foreach (var (key, packet) in data.MobEntitySavePackets) {
					if (this._saveableMobEntities.TryGetValue(key, out var provider)) {
						provider.LoadEntitySavePacket(packet);
					} else {
						Debug.LogWarning($"[EntityRegistry] No active Mob save provider registered for saved ID: {key}. Skipping load.");
					}
				}
			}

			if (data.PropEntitySavePackets != null) {
				foreach (var (key, packet) in data.PropEntitySavePackets) {
					if (this._saveablePropEntities.TryGetValue(key, out var provider)) {
						provider.LoadEntitySavePacket(packet);
					} else {
						Debug.LogWarning($"[EntityRegistry] No active Prop save provider registered for saved ID: {key}. Skipping load.");
					}
				}
			}
		}

		#endregion
	}
}