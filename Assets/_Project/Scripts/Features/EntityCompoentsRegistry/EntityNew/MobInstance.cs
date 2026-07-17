using UnityEngine;
using Kope.EntityIdentity;

namespace Kope.Core.Identity {

	public class MobInstance : EntityInstanceNew {
		[SerializeField] private EntityRelation relation;
		[SerializeField] private RaceEnum race;
		[SerializeField] private GenderEnum gender;

		public override EntityType Type => EntityType.MOB;
		private MobConfig _cachedConfig;
		private MobEntityDetail _cachedDetail;
		public override EntityConfig Config {
			get {
				this._cachedConfig ??= new MobConfig(entityName, relation, race, gender);
				return this._cachedConfig;
			}
		}

		public override EntityDetailBase EntityDetail {

			get {
				this._cachedDetail ??= new MobEntityDetail(
					this.uniqueID, (MobConfig)this.Config, this.ComponentsRegistryForSaveSystemOnly, this);
				return this._cachedDetail;
			}
		}
	}
}