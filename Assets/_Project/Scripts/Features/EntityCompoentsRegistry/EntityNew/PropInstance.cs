using System;
using Kope.Core.Collections.Hashes;
using Kope.Core.EntityComponentRegistry;
using Kope.EntityIdentity;
using UnityEngine;

namespace Kope.Core.Identity {

	public interface IPropEntityInstance {
		PropEntityDetail PropEntityDetail { get; }
		void InvokeOnEntityDiedOrPooledEvent();
	}
	public class PropInstance : EntityInstanceNew, IPropEntityInstance, IPropEntityDiedOrPooled {
		[SerializeField] private PropType propType;
		[SerializeField] private EntityNature nature = EntityNature.STATIC;

		private PropConfig _cachedConfig;
		private PropEntityDetail _entityDetail;
		private HashedTag _propGroupHashTag;

		private event Action<PropEntityDetail> OnPropEntityDiedOrPooled;


		public override EntityType Type => EntityType.PROP;

		public PropType PropType => propType;
		public EntityNature Nature => nature;
		public HashedTag TypeGroupHashTag => _propGroupHashTag;
		public PropConfig PropConfig =>
		 this._cachedConfig ??= new PropConfig(entityName, propType, nature);

		public PropEntityDetail PropEntityDetail => this._entityDetail ??= new PropEntityDetail(
			this.uniqueID,
			this.PropConfig,
			this.ecr.ComponentRegistry,
			this
		);

		public void OnEntityDiedOrPooledEvent(Action<PropEntityDetail> callback, bool isSubscribe) {
			this.OnPropEntityDiedOrPooled -= callback;
			if (isSubscribe) this.OnPropEntityDiedOrPooled += callback;
		}
		public void InvokeOnEntityDiedOrPooledEvent() {
			this.OnPropEntityDiedOrPooled?.Invoke(this.PropEntityDetail);
		}
	}
}