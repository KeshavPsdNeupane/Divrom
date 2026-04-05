using System;
using System.Collections.Generic;
using Kope.Core.Entity;
using Newtonsoft.Json;
using Unity.VectorGraphics;

namespace Kope.SaveSystem {

	// ----------------------------------GLOBAL SAVE SYSTEM RELATED INTERFACES AND CLASSES----------------------------------
	#region  Scene Save System Related Interfaces and Classes

	/*
    Core interaction between GlobalSaveSystem (GSS) and SceneSaveSystem (SSS):

		1. Each SSS holds a reference to GSS.
		2. SSS does NOT register itself into GSS (no ownership coupling).
		3. SSS decides *when* a save/load should happen and calls:
			- GSS.SaveTheScene(sceneData)
			- GSS.LoadTheScene(sceneId)
		passing or receiving a SceneSaveDataContainer.

		→ Control flow is inverted:
			SSS drives execution timing,
			GSS only provides persistence logic.

		4. On save:
			SSS builds and passes a SceneSaveDataContainer to GSS.
		On load:
			SSS requests data (by index/name),
			GSS returns the container,
			SSS applies it to the scene.

		Result:
		- Clear separation of responsibilities:
			SSS → lifecycle & timing
			GSS → storage & retrieval
		- No tight coupling or registration overhead
		- Easily extensible for multiple scenes sharing one global system
	*/

	/// <summary>
	/// Aggregate structure for scene save data, containing the scene index, name, and a dictionary of 
	/// save data containers categorized by provider type.
	/// This struct serves as a comprehensive package of all the save data related to a specific scene, 
	/// allowing for organized storage and retrieval of scene data during the save and load processes.
	/// The SceneSaveDataAggregate can be used to encapsulate all the necessary information about a scene's 
	/// save state, making it easier to manage and maintain the save data for different scenes in a structured way. 
	/// By categorizing the save data by provider type, it allows for efficient access to specific types of data when needed, and
	/// provides a clear structure for how scene data is organized and stored in the save system.
	/// </summary>
	public readonly struct SceneSaveDataAggregate {
		public readonly int SceneIndex;
		public readonly string SceneName;
		public readonly Dictionary<SceneDataProviderTypeEnum, SceneSaveDataContainer> SceneDataByProvider;
		[JsonConstructor]
		public SceneSaveDataAggregate(int sceneIndex, string sceneName, Dictionary<SceneDataProviderTypeEnum, SceneSaveDataContainer> sceneDataByProvider) {
			SceneIndex = sceneIndex;
			SceneName = sceneName;
			SceneDataByProvider = sceneDataByProvider;
		}
	}



	/// <summary>
	/// Defines the types of scene data providers that can be used in the save system.
	/// This enum can be used to categorize different types of data providers, allowing the save system to
	/// handle different types of save data in a structured way. For example,
	/// the EntityRegistry provider type can be used to identify providers that are responsible for saving 
	/// and loading entity-related data in the scene, while other provider types can be added in the future 
	/// to handle different aspects of the scene data as needed.
	/// for now we only have 1 provider type, but we can easily add more in the future if we need to 
	/// support more types of data providers in the save system.
	/// </summary>
	public enum SceneDataProviderTypeEnum {
		None = 0,
		EntityRegistry = 1,
	}



	/// <summary>
	/// Used by EntityRegistry to provide the save data for each entity in the scene,
	/// </summary>
	public interface ISceneSaveProvider {
		SceneDataProviderTypeEnum ProviderType { get; }
		SceneSaveDataContainer OnSave();
		void OnLoad(SceneSaveDataContainer data);
	}

	public class SceneSaveDataContainer {
		public SceneDataProviderTypeEnum ProviderType { get; private set; }
		public Dictionary<HashedTag, EntitySavePacket> EntitySavePackets { get; private set; }

		[JsonConstructor]
		public SceneSaveDataContainer(
			SceneDataProviderTypeEnum providerType,
			Dictionary<HashedTag, EntitySavePacket> entitySavePackets) {
			this.ProviderType = providerType;
			EntitySavePackets = entitySavePackets;
		}
	}

	#endregion

	// ----------------------------------Scene SAVE SYSTEM RELATED INTERFACES AND CLASSES----------------------------------

	#region Entity Save System Related Interfaces and Classes

	/// <summary>
	/// Defines interfaces for save data and identity providers in the context of a save system.
	/// <para>
	/// The IIdentityProvider interface is implemented by entities that can provide a common name hash tag and
	/// a unique ID hash tag, which can be used for identification and organization in the save system.
	/// The ISaveData interface is a marker interface for classes that represent save data, allowing for type safety and organization of save-related data structures.
	/// The ISaveable interface is implemented by ECS components (MonoBehaviours) that can be saved and loaded, providing methods to get save data and load from save data.
	/// </para>
	/// </summary>
	public interface IEntitySavePacketProvider {
		HashedTag UniqueID { get; }
		EntitySavePacket GetEntitySavePacket();
		void LoadEntitySavePacket(EntitySavePacket packet);
		bool ValidateIdentity(string callerInfo = null);
		void RegisterSaveDataChunk();
	}

	[Serializable]
	public class EntitySavePacket {
		public HashedTag CommonNameHashTag { get; private set; }
		public EntityIdentityCategoryEnum Category { get; private set; }
		public HashedTag UniqueID { get; private set; }

		// the type will be used to identify the source of the save data, 
		// and the save system will use the type to find the corresponding provider to load the data.
		public Dictionary<Type, ISaveData> Data = new();

		[JsonConstructor]
		public EntitySavePacket(
		HashedTag commonNameHashTag,
		EntityIdentityCategoryEnum category,
		HashedTag uniqueID,
		Dictionary<Type, ISaveData> Data) {
			this.CommonNameHashTag = commonNameHashTag;
			this.Category = category;
			this.UniqueID = uniqueID;
			this.Data = Data;
		}
	}

	#endregion



	#region Component Level Save System Related Interfaces and Classes
	/// <summary>
	/// Interface for objects that can be saved and loaded using the save system.
	/// Defines methods for getting save data and loading from save data, allowing implementing classes to specify how they should be saved and loaded.
	/// This interface can be implemented by any class that needs to support saving and loading of its state, providing a standardized way for the save system to interact with different types of saveable objects.
	/// By implementing this interface, classes can define their own logic for how they generate save data and how they restore their state from save data, while still adhering to a common contract that the save system can work with.
	/// </summary>
	public interface ISaveable {
		/// <summary>
		/// We might need something like a database in future that will,
		/// manage componentId(this will include name+version) -> ComponentType mapping, so that we can just
		///  save the componentId in the save data,
		/// and then use the database to find the corresponding component type when loading the data,
		/// this way we can avoid the problem of type name changes and assembly changes that can break 
		/// the save data when we are doing development and refactoring.
		/// But for now we will just use the component type as the key in the save data dictionary, 
		/// since we dont have that many components and we are still in early development stage where 
		/// we are still iterating on the component design and structure,
		/// so we will just accept the risk of save data being broken during development. we can just resave.
		/// The database can be a SO i think, since we dont need to modify it at runtime, we just need to read from 
		/// it when loading the data,
		/// and we can have a editor tool to generate the database SO asset based on the current components in the project, 
		/// so that we can easily keep the database up to date with the current components in the project.
		/// and if we added any dlc we can just patch the SO database asset with the new components in the dlc, 
		/// so that we can easily support the new components in the dlc without breaking the existing save data.
		/// </summary>
		/// <returns></returns>
		ISaveData GetSaveData();
		void LoadFromSaveData(ISaveData data);
	}

	/// <summary>
	/// Marker interface for save data classes.
	/// Does not define any members, but serves as a common type for all save data structures in the save system.
	/// This allows for type safety and organization of save-related data, as all save data classes can
	/// implement this interface to indicate that they are intended for use as save data, and
	/// can be easily identified and managed within the save system.
	/// But when we are evaluating the interfact the struct will be boxed but it is not a problem since we are
	/// only using it for data storage and retrieval, and the performance impact of boxing is negligible in this context.
	/// </summary>
	public interface ISaveData {
	}
	#endregion

}