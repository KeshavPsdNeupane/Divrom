using Kope.Core.Collections.UniqueId;
using Kope.Core.EntityComponentRegistry;
using Kope.EntityIdentity;

namespace Kope.Core.Identity {

	public class MobEntityDetail {
		// simple class that can be made
		public UniqueID UniqueID { get; private set; }
		// simple hashed tag imple
		public MobConfig MobConfig { get; private set; }
		public IReadOnlyComponentRegistry ComponentRegistry { get; private set; }
		// every entity with entity manager will have this 
		public readonly IMobEntityDiedOrPooled EventProvider;

		// so this class is a pure data class
		// the dependency is on only this ECS architecture,
		// but it is not dependent on any specific system or feature, 
		// so it can be used across the entire project without creating circular dependencies

		public MobEntityDetail(
			UniqueID uniqueID,
			MobConfig mobConfig,
			IReadOnlyComponentRegistry entityComponentRegistry,
			IMobEntityDiedOrPooled onEntityDiedOrPooled) {
			this.UniqueID = uniqueID;
			this.MobConfig = mobConfig;
			this.ComponentRegistry = entityComponentRegistry;
			this.EventProvider = onEntityDiedOrPooled;
		}
	}
	public class PropEntityDetail {
		// simple class that can be made
		public UniqueID UniqueID { get; private set; }
		public PropConfig PropConfig { get; private set; }
		public IReadOnlyComponentRegistry ComponentRegistry { get; private set; }
		// every entity with entity manager will have this 
		public readonly IPropEntityDiedOrPooled EventProvider;

		// so this class is a pure data class
		// the dependency is on only this ECS architecture,
		// but it is not dependent on any specific system or feature, 
		// so it can be used across the entire project without creating circular dependencies

		public PropEntityDetail(
			UniqueID uniqueID,
			PropConfig propConfig,
			IReadOnlyComponentRegistry entityComponentRegistry,
			IPropEntityDiedOrPooled onEntityDiedOrPooled) {
			this.UniqueID = uniqueID;
			this.PropConfig = propConfig;
			this.ComponentRegistry = entityComponentRegistry;
			this.EventProvider = onEntityDiedOrPooled;
		}
	}
}