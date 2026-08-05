using UnityEngine;
using Kope.EntityIdentity;


namespace Kope.Core.Identity {
	public class PropInstance : EntityInstance {
		[SerializeField] private PropType propType;
		[SerializeField] private EntityNature nature = EntityNature.STATIC;

		private PropEntityDetail _cachedDetail;
		public override EntityType Type => EntityType.PROP;
		private PropConfig _cachedConfig;

		public override EntityConfig Config {
			get {
				this._cachedConfig ??= new PropConfig(entityName, propType, nature);
				return this._cachedConfig;
			}
		}
		public override EntityDetailBase EntityDetail {
			get {
				this._cachedDetail ??= new PropEntityDetail(this.uniqueID,
				(PropConfig)this.Config, this.ComponentsRegistryForSaveSystemOnly, this);
				return this._cachedDetail;
			}
		}
	}
}