using System;
using System.Collections.Generic;
using Kope.Core.Collections.Hashes;
using Kope.EntityIdentity;
using Newtonsoft.Json;

/* --- KOPE SAVE SYSTEM ARCHITECTURAL GUIDELINES ---
 * 
 * 1. AVOID BOXING OVERHEAD: 
 *    Implementations of the ISaveData interface MUST be declared as reference types (classes). 
 *    Using structs introduces severe boxing/unboxing overhead when passing payloads through 
 *    the component identity data maps or tracking state allocations within runtime collections.
 *
 * 2. DECOUPLE PERSISTENCE STRUCTS FROM REFLECTION NAMES:
 *    Always use explicit [JsonProperty("id")] tokens to divorce saved file states from C# identifiers. 
 *    This allows you to rename 'Position' to 'WorldPos' inside structural scripts safely without 
 *    invalidating legacy serialized save data.
 *    EXAMPLE: [JsonProperty("p")] public Vec3 Position; // JSON layout always preserves "p"
 *
 * 3. DECOUPLE RUNTIME COMPONENT TYPES FROM STRING MATCHES:
 *    Component identifiers inside the serialization map are processed through a mapping registry.
 *    This ensures that changing namespaces or moving target scripts to alternative assembly definitions 
 *    will not corrupt save payloads.
 */

namespace Kope.SaveSystem {

	// ==================================================================================
	// GLOBAL & SCENE SAVE SYSTEM ARCHITECTURE
	// ==================================================================================
	#region Scene Save System Core Contracts

	/*
     * INTERACTION MODEL: GlobalSaveSystem (GSS) <-> SceneSaveSystem (SSS)
     *
     * 1. SSS manages room/scene lifecycle timing and structural instantiation scopes.
     * 2. SSS does NOT couple state management to the GSS runtime layout via tight lifecycle registration.
     * 3. SSS drives execution flow, determining when transitions occur via explicit execution calls:
     *    - GSS.SaveTheScene(sceneDataAggregate)
     *    - GSS.LoadTheScene(sceneId)
     *
     * Inversion of Control:
     * - SSS maps local component context, lifecycle conditions, and execution timing.
     * - GSS purely handles disk persistence logic, data stream formatting, and cache retrieval.
     */

	/// <summary>
	/// Aggregate data capsule housing metadata and functional data blocks belonging to a target scene layout.
	/// </summary>
	[Serializable]
	public readonly struct SceneSaveDataAggregate {
		[JsonProperty("sIndex")]
		public readonly int SceneIndex;

		[JsonProperty("sName")]
		public readonly string SceneName;

		[JsonProperty("sD")]
		public readonly Dictionary<SceneDataProviderTypeEnum, SceneSaveDataContainer> SceneDataByProvider;

		[JsonConstructor]
		public SceneSaveDataAggregate(
			int sceneIndex,
			string sceneName,
			Dictionary<SceneDataProviderTypeEnum, SceneSaveDataContainer> sceneDataByProvider) {

			this.SceneIndex = sceneIndex;
			this.SceneName = sceneName;
			this.SceneDataByProvider = sceneDataByProvider;
		}
	}

	/// <summary>
	/// Identifies the explicit category designation for functional save data handlers.
	/// Enables downstream optimization loops to partition execution paths safely.
	/// </summary>
	public enum SceneDataProviderTypeEnum {
		None = 0,
		EntityRegistry = 1,
	}

	/// <summary>
	/// Contract implemented by scene-level data persistence coordinators to compile or distribute state changes.
	/// </summary>
	public interface ISceneSaveProvider {
		SceneDataProviderTypeEnum ProviderType { get; }
		SceneSaveDataContainer OnSaveNew();
		void OnLoadNew(SceneSaveDataContainer data);
	}

	/// <summary>
	/// Data structure enclosing compiled entity serialization packets mapped by their specific layout variations.
	/// </summary>
	[Serializable]
	public struct SceneSaveDataContainer {
		[JsonProperty("provType")]
		public SceneDataProviderTypeEnum ProviderType { get; private set; }

		[JsonProperty("mobPackets")]
		public Dictionary<HashedTag, MobEntitySavePacket> MobEntitySavePackets { get; private set; }

		[JsonProperty("propPackets")]
		public Dictionary<HashedTag, PropEntitySavePacket> PropEntitySavePackets { get; private set; }

		[JsonConstructor]
		public SceneSaveDataContainer(
			SceneDataProviderTypeEnum providerType,
			Dictionary<HashedTag, MobEntitySavePacket> mobEntitySavePackets,
			Dictionary<HashedTag, PropEntitySavePacket> propEntitySavePackets) {

			this.ProviderType = providerType;
			this.MobEntitySavePackets = mobEntitySavePackets;
			this.PropEntitySavePackets = propEntitySavePackets;
		}
	}

	#endregion

	// ==================================================================================
	// ENTITY CORE PROVIDER AND SERIALIZATION LAYOUT
	// ==================================================================================
	#region Entity Save System Contracts

	/// <summary>
	/// Agnostic infrastructure interface allowing structural storage systems to poll 
	/// basic registration lifecycles without coupling to generic packet arguments.
	/// </summary>
	public interface ISavableEntityProvider {
		HashedTag UniqueID { get; }
		void RegisterSaveDataChunk();
	}

	/// <summary>
	/// Unified generic provider contract managing entity state capture and allocation loops.
	/// </summary>
	public interface ISavableEntityProvider<TPacket> : ISavableEntityProvider {
		TPacket GetEntitySavePacket();
		void LoadEntitySavePacket(TPacket packet);
	}

	/// <summary>
	/// Interface signature specifically handling data mapping hooks for Mob architectures.
	/// </summary>
	public interface IMobEntitySavePacketProvider : ISavableEntityProvider<MobEntitySavePacket> { }

	/// <summary>
	/// Interface signature specifically handling data mapping hooks for Prop architectures.
	/// </summary>
	public interface IPropEntitySavePacketProvider : ISavableEntityProvider<PropEntitySavePacket> { }

	/// <summary>
	/// Serialization packet containing discrete component state modifications and configuration metadata for a Mob.
	/// </summary>
	[Serializable]
	public struct MobEntitySavePacket {
		[JsonProperty("uid")]
		public HashedTag UniqueID { get; private set; }

		[JsonProperty("identity")]
		public MobConfig Config { get; private set; }

		[JsonProperty("data")]
		public Dictionary<string, ISaveData> Data;

		[JsonConstructor]
		public MobEntitySavePacket(HashedTag uniqueID, MobConfig config, Dictionary<string, ISaveData> data) {
			this.UniqueID = uniqueID;
			this.Config = config;
			this.Data = data;
		}
	}

	/// <summary>
	/// Serialization packet containing discrete component state modifications and configuration metadata for a Prop.
	/// </summary>
	[Serializable]
	public struct PropEntitySavePacket {
		[JsonProperty("uid")]
		public HashedTag UniqueID { get; private set; }

		[JsonProperty("identity")]
		public PropConfig TypeGroupConfig { get; private set; }

		[JsonProperty("data")]
		public Dictionary<string, ISaveData> Data;

		[JsonConstructor]
		public PropEntitySavePacket(HashedTag uniqueID, PropConfig typeGroupConfig, Dictionary<string, ISaveData> data) {
			this.UniqueID = uniqueID;
			this.TypeGroupConfig = typeGroupConfig;
			this.Data = data;
		}
	}

	#endregion

	// ==================================================================================
	// COMPONENT LEVEL PERSISTENCE DEFINITIONS
	// ==================================================================================
	#region Component Level Save System Interfaces

	/// <summary>
	/// Contract applied to internal ECS components (MonoBehaviours) requiring persistent state tracking.
	/// </summary>
	public interface ISaveable {
		/// <summary>
		/// Generates an allocation snapshot block containing the component's state parameters.
		/// </summary>
		ISaveData GetSaveData();

		/// <summary>
		/// Restores target variables back to the component using a parsed data payload chunk.
		/// </summary>
		void LoadFromSaveData(ISaveData data);
	}

	/// <summary>
	/// Structural marker interface for data packets representing saved component parameters.
	/// Classes implementing this contract MUST be reference types to block boxing allocations.
	/// </summary>
	public interface ISaveData { }

	#endregion
}