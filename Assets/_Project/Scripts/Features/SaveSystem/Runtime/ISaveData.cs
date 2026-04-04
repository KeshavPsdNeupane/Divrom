using System;
using System.Collections.Generic;
using Kope.Core.Entity;
using Newtonsoft.Json;


namespace Kope.Core.SaveSystem {

	public interface ISceneSaveSystemProvider {
		SceneSaveDataContainer GetSceneSaveSystem();
	}

	public class SceneSaveDataContainer {
		// we will mostly use Index to identify the scene since it is more stable than name, 
		// but we will also store the name for debugging and editor purpose.
		public int SceneBuildIndex { get; private set; }
		public string SceneName { get; private set; }
		public List<EntitySavePacket> SceneDataPacket { get; private set; }
		[JsonConstructor]
		public SceneSaveDataContainer(int sceneBuildIndex, string sceneName, List<EntitySavePacket> sceneDatas) {
			SceneBuildIndex = sceneBuildIndex;
			SceneName = sceneName;
			SceneDataPacket = sceneDatas;
		}
	}
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

		// Store the DATA, not the Component class
		public Dictionary<Type, ISaveData> DataSource = new();

		[JsonConstructor]
		public EntitySavePacket(
		HashedTag commonNameHashTag,
		EntityIdentityCategoryEnum category,
		HashedTag uniqueID,
		Dictionary<Type, ISaveData> dataSource) {
			this.CommonNameHashTag = commonNameHashTag;
			this.Category = category;
			this.UniqueID = uniqueID;
			this.DataSource = dataSource;
		}
	}

	/// <summary>
	/// Interface for objects that can be saved and loaded using the save system.
	/// Defines methods for getting save data and loading from save data, allowing implementing classes to specify how they should be saved and loaded.
	/// This interface can be implemented by any class that needs to support saving and loading of its state, providing a standardized way for the save system to interact with different types of saveable objects.
	/// By implementing this interface, classes can define their own logic for how they generate save data and how they restore their state from save data, while still adhering to a common contract that the save system can work with.
	/// </summary>
	public interface ISaveable {
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
}