using System.Collections.Generic;
using Kope.Core.Identity;
using Kope.Core.Types.Hashes;
using Kope.Core.ServiceLocator;

namespace Kope.EntityComponentSystem.Unused {

	/// <summary>
	/// The SceneEntityRegistry serves as a centralized registry for all EntityInstances in the scene, 
	/// allowing for easy registration and unregistration of entities as they are created and destroyed.
	/// <br/>
	/// The SceneEntityRegistry maintains a dictionary of registered entities, where the key is 
	/// the UniqueID of the entity (represented as a HashedTag) and the value is the corresponding EntityInstance.
	/// This allows for efficient retrieval and management of entities in the scene, as well as 
	/// providing a convenient way for systems to access and interact with entities through their UniqueIDs. 
	/// Systems can query the SceneEntityRegistry to get references to specific entities based on 
	/// their UniqueIDs, enabling a decou
	/// pled architecture where systems can interact with entities without needing direct references 
	/// to them, and instead relying on the registry to manage the entity references and provide access to them when needed.
	/// <br/>
	/// NOTE : THIS IS NOT BEING USED SINCE THE IDEA IS HERE BUT THE NEED OF THE SYSTEM IS NOT VERY STRONG, 
	/// AND IT CAN BE EASILY IMPLEMENTED IN THE FUTURE IF NEEDED, BUT FOR NOW IT'S JUST 
	/// AN EXAMPLE OF HOW WE CAN IMPLEMENT A SCENE ENTITY REGISTRY IF WE NEED TO.
	/// </summary>
	public class SceneEntityRegistry : SceneServiceBase {
		private readonly Dictionary<HashedTag, EntityInstance> _registeredEntities = new();
		public void RegisterEntity(EntityInstance entity) {
			if (entity == null) return;
			var id = entity.EntityDetail.UniqueID.HashedTag;
			if (this._registeredEntities.ContainsKey(id)) return;
			this._registeredEntities.Add(id, entity);
		}
		public void UnregisterEntity(HashedTag entityId) {
			if (this._registeredEntities.ContainsKey(entityId)) {
				this._registeredEntities.Remove(entityId);
			}
		}
		public bool TryGetEntity(HashedTag entityId, out EntityInstance entity) {
			return this._registeredEntities.TryGetValue(entityId, out entity);
		}
	}
}
