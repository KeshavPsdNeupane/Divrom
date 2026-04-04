using System;

namespace Kope.Core.Entity {
	/// <summary>
	/// Interface for entities that can trigger events when they die or are pooled.
	/// <para>
	/// <b>OnEntityDied:</b> This event is triggered when the entity dies, allowing subscribers to perform cleanup, update contexts, or trigger other game logic in response to the
	/// </summary>
	public interface IEntityDiedOrPooled {
		/// <summary>
		/// Event triggered when the entity dies or is pooled. Subscribers can use this event to perform necessary
		///  cleanup, update contexts, or trigger other game logic in response to the entity's death or pooling.
		/// The UniqueID parameter identifies the specific entity that died or was pooled, while the HashedTag
		///  parameter provides additional context about the type or category of the entity, allowing subscribers
		///  to differentiate between different kinds of entities and respond accordingly.
		/// The interface is combined since we need to do same operations on both death and pooling,
		///  such as removing the entity from contexts, and having a single event simplifies the subscription
		///  and handling logic for these cases. the only difference being the GameObject is either destroyed (death)
		///  or deactivated and returned to pool (pooled), but in both cases the entity is no 
		/// longer active in the game world and needs to be handled similarly by subscribers.
		/// </summary>
		event Action<EntityDetail> OnEntityDiedOrPooled;
	}
}
