using UnityEngine;
using Kope.EntityIdentity;
using Kope.Core.EntityComponentRegistry;

namespace Kope.Core.Identity {
	public interface IPropEntityInstance : IEntityDetailProvider<PropEntityDetail> { }

	public class PropInstance : EntityInstanceNew<PropConfig, PropEntityDetail>, IPropEntityInstance, IPropEntityDiedOrPooled {
		[SerializeField] private PropType propType;
		[SerializeField] private EntityNature nature = EntityNature.STATIC;

		public override EntityType Type => EntityType.PROP;

		protected override PropConfig CreateConfig() => new(entityName, propType, nature);
		protected override PropEntityDetail CreateEntityDetail() => new(uniqueID, Config, ecr.ComponentRegistry, this);
	}
}