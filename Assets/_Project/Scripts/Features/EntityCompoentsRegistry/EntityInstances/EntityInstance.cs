using System;
using UnityEngine;
using Kope.Core.EntityComponentRegistry;
using Kope.Core.Collections.UniqueId;
using Kope.Core.Collections.Hashes;
using Kope.Core.Collections.Extensions;
using Kope.Core.LifeTimeManagement;
using Kope.Core.ServiceLocator;
using Kope.EntityIdentity;
using Kope.EntityComponentSystem;

namespace Kope.Core.Identity {
	public interface IEntityInstance {
		EntityType Type { get; }
		public HashedTag UniqueTag { get; }
		public EntityConfig Config { get; }
		public EntityDetailBase EntityDetail { get; }
		public ComponentRegistry ComponentsRegistryForSaveSystemOnly { get; }
	}

	[RequireComponent(typeof(UniqueID))]
	public abstract class EntityInstance :
	InitializableBase, IEntityInstance, IEntityDiedOrPooledNew {

		[SerializeField] protected string entityName;
		[SerializeField] protected UniqueID uniqueID;
		[SerializeField] protected EntityComponentsRegistry ecr;

		private event Action<EntityDetailBase> OnEntityDiedOrPooled;

		private EntityRegistry _entityRegistry;
		public ComponentRegistry ComponentsRegistryForSaveSystemOnly => this.ecr.ComponentRegistry;

		public abstract EntityType Type { get; }
		public abstract EntityConfig Config { get; }
		public abstract EntityDetailBase EntityDetail { get; }

		public HashedTag UniqueTag {
			get {
				if (this.EntityDetail == null || this.EntityDetail.UniqueID == null) return default;
				return this.EntityDetail.UniqueID.HashedTag;
			}
		}

		protected override bool OnInit() {
			if (!Validate()) return false;

			RegisterToEntityRegistry();
			return true;
		}

		// Independent of EntitySaveSystemBase's own registration into EntityRegistry -
		// this entity doesn't know or care whether it's savable at all.
		private void RegisterToEntityRegistry() {
			if (!SceneServiceLocator.Instance.TryGetService(out this._entityRegistry)) {
				Debug.LogError($"[{GetType().Name}] No EntityRegistry found in the scene. Please ensure " +
					$"that an EntityRegistry is present and properly initialized in the scene for entity " +
					$"tracking to function correctly. {this.GetFullHierarchyPath()}", this.gameObject);
				return;
			}

			this._entityRegistry.RegisterEntity(this);
		}

		void OnEnable() => OnEntityDiedOrPooledEvent(UnregisterFromEntityRegistry, true);
		void OnDisable() => OnEntityDiedOrPooledEvent(UnregisterFromEntityRegistry, false);

		private void UnregisterFromEntityRegistry(EntityDetailBase detail) {
			if (this._entityRegistry == null) return;
			this._entityRegistry.UnregisterEntity(this);
		}

		public void InvokeOnEntityDiedOrPooledEvent() => this.OnEntityDiedOrPooled?.Invoke(this.EntityDetail);
		private bool Validate() => this.uniqueID != null && this.ecr != null && this.ecr.ComponentRegistry != null;

		public void OnEntityDiedOrPooledEvent(Action<EntityDetailBase> callback, bool isSubscribe) {
			this.OnEntityDiedOrPooled -= callback;
			if (isSubscribe) this.OnEntityDiedOrPooled += callback;
		}
	}
}