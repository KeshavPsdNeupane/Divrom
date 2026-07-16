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

	public interface IEntityDetailProvider<out TDetail> {
		TDetail EntityDetail { get; }
		void InvokeOnEntityDiedOrPooledEvent();
	}

	[RequireComponent(typeof(UniqueID))]
	public abstract class EntityInstanceNew<TConfig, TDetail> : InitializableBase, IEntityInstance, ISavableEntity, IEntityDetailProvider<TDetail>
		where TConfig : EntityConfig
		where TDetail : IEntityDetail {

		[SerializeField] protected string entityName;
		[SerializeField] protected UniqueID uniqueID;
		[SerializeField] protected EntityComponentsRegistry ecr;

		private bool _isSavable = false;
		private TConfig _cachedConfig;
		private TDetail _cachedDetail;
		private event Action<TDetail> OnEntityDiedOrPooled;

		public bool IsSavable => _isSavable;
		public ComponentRegistry ComponentsRegistryForSaveSystemOnly => ecr.ComponentRegistry;

		public abstract EntityType Type { get; }
		public TConfig Config => _cachedConfig ??= CreateConfig();
		public TDetail EntityDetail => _cachedDetail ??= CreateEntityDetail();

		protected abstract TConfig CreateConfig();
		protected abstract TDetail CreateEntityDetail();

		protected override bool OnInit() => Validate();
		public void SetSavable(bool isSavable) => _isSavable = isSavable;

		public void OnEntityDiedOrPooledEvent(Action<TDetail> callback, bool isSubscribe) {
			OnEntityDiedOrPooled -= callback;
			if (isSubscribe) OnEntityDiedOrPooled += callback;
		}

		public void InvokeOnEntityDiedOrPooledEvent() => OnEntityDiedOrPooled?.Invoke(EntityDetail);

		private bool Validate() => uniqueID != null && ecr != null && ecr.ComponentRegistry != null;
	}
}