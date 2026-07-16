using UnityEngine;
using Kope.EntityIdentity;
using Kope.Core.EntityComponentRegistry;

namespace Kope.Core.Identity {
	public interface IMobEntityDetail : IEntityDetailProvider<MobEntityDetail> { }

	public class MobInstance : EntityInstanceNew<MobConfig, MobEntityDetail>, IMobEntityDetail, IMobEntityDiedOrPooled {
		[SerializeField] private EntityRelation relation;
		[SerializeField] private RaceEnum race;
		[SerializeField] private GenderEnum gender;

		public override EntityType Type => EntityType.MOB;

		protected override MobConfig CreateConfig() => new(entityName, relation, race, gender);
		protected override MobEntityDetail CreateEntityDetail() => new(uniqueID, Config, ecr.ComponentRegistry, this);
	}
}