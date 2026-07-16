using UnityEngine;
using Kope.Core.EntityComponentRegistry;
using Kope.Core.Collections.UniqueId;
using Kope.Core.LifeTimeManagement;
using Kope.EntityIdentity;
using Kope.EntityComponentSystem;
using Kope.Core.ServiceLocator;

namespace Kope.Core.Identity {
	public interface IEntityInstance {
		EntityType Type { get; }
	}
	public interface ISavableEntity {
		bool IsSavable { get; }
		void SetSavable(bool isSavable);
	}

	[RequireComponent(typeof(UniqueID))]
	public abstract class EntityInstanceNew : InitializableBase, IEntityInstance, ISavableEntity {
		[SerializeField] protected string entityName;
		[SerializeField] protected UniqueID uniqueID;
		[SerializeField] protected EntityComponentsRegistry ecr;

		private bool _isSavable = false;
		public bool IsSavable => _isSavable;
		public ComponentRegistry ComponentsRegistryForSaveSystemOnly => ecr.ComponentRegistry;
		public abstract EntityType Type { get; }
		protected override bool OnInit() {
			if (!Validate()) return false;
			return true;
		}
		public void SetSavable(bool isSavable) => _isSavable = isSavable;
		private bool Validate() {
			if (this.uniqueID == null || this.ecr == null || this.ecr.ComponentRegistry == null) return false;
			return true;
		}
	}
}