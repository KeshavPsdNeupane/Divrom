using Kope.Core.Identity;
using Kope.Core.Collections.Hashes;
using Kope.Core.Collections.UniqueId;

namespace Kope.Core.EntityComponentRegistry {
	/// <summary>
	/// This class serves as a simple data container for essential details about an Entity in the ECS architecture. 
	/// It encapsulates the UniqueID, CommonEntityHashedTag, and EntityComponentRegistry associated with an Entity.
	///  This class is used to store and pass around these critical pieces of information together, especially 
	/// when notifying systems about an Entity's death or pooling events through the OnEntityDiedOrPooled event 
	/// in the EntityManager. By using this class, we can ensure that all relevant details about an Entity are 
	/// easily accessible and organized when needed for various operations within the ECS framework.
	/// </summary>
	public class EntityDetail {
		// simple class that can be made
		public UniqueID UniqueID { get; private set; }
		// simple hashed tag imple
		public HashedTag CommonEntityHashedTag { get; private set; }

		public EntityIdentityCategoryEnum EntityIdentityCategoryEnum { get; private set; }
		public IReadOnlyComponentRegistry ComponentRegistry { get; private set; }
		// every entity with entity manager will have this 
		public readonly IEntityDiedOrPooled EventProvider;

		// so this class is a pure data class
		// the dependency is on only this ECS architecture,
		// but it is not dependent on any specific system or feature, 
		// so it can be used across the entire project without creating circular dependencies

		public EntityDetail(
			UniqueID uniqueID,
			HashedTag commonEntityHashedTag,
			IReadOnlyComponentRegistry entityComponentRegistry,
			IEntityDiedOrPooled onEntityDiedOrPooled,
			EntityIdentityCategoryEnum cate = EntityIdentityCategoryEnum.Player) {
			this.UniqueID = uniqueID;
			this.CommonEntityHashedTag = commonEntityHashedTag;
			this.ComponentRegistry = entityComponentRegistry;
			this.EventProvider = onEntityDiedOrPooled;
			this.EntityIdentityCategoryEnum = cate;
		}
	}
}
