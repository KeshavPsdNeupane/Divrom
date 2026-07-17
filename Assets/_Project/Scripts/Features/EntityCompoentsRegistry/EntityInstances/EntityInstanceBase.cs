using System;
using UnityEngine;
using Kope.Core.EntityComponentRegistry;
using Kope.Core.Collections.UniqueId;
using Kope.Core.LifeTimeManagement;
using Kope.EntityIdentity;

namespace Kope.Core.Identity {
	public interface IEntityInstance {
		EntityType Type { get; }
	}
	public interface ISavableEntity {
		bool IsSavable { get; }
		void SetSavable(bool isSavable);
	}

	[RequireComponent(typeof(UniqueID))]
	public abstract class EntityInstanceNew :
	InitializableBase, IEntityInstance, ISavableEntity, IEntityDiedOrPooledNew {

		[SerializeField] protected string entityName;
		[SerializeField] protected UniqueID uniqueID;
		[SerializeField] protected EntityComponentsRegistry ecr;

		private bool _isSavable = false;

		private event Action<EntityDetailBase> OnEntityDiedOrPooled;

		public bool IsSavable => this._isSavable;
		public ComponentRegistry ComponentsRegistryForSaveSystemOnly => this.ecr.ComponentRegistry;

		public abstract EntityType Type { get; }
		public abstract EntityConfig Config { get; }
		public abstract EntityDetailBase EntityDetail { get; }

		protected override bool OnInit() => Validate();
		public void SetSavable(bool isSavable) => this._isSavable = isSavable;


		public void InvokeOnEntityDiedOrPooledEvent() => this.OnEntityDiedOrPooled?.Invoke(this.EntityDetail);
		private bool Validate() => this.uniqueID != null && this.ecr != null && this.ecr.ComponentRegistry != null;

		public void OnEntityDiedOrPooledEvent(Action<EntityDetailBase> callback, bool isSubscribe) {
			this.OnEntityDiedOrPooled -= callback;
			if (isSubscribe) this.OnEntityDiedOrPooled += callback;
		}
	}
}