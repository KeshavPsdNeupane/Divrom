using System;
using System.Collections.Generic;
using UnityEngine;
using Kope.Core.SaveSystem;
using ServiceLocatorPattern;

namespace Kope.Core.Identity {
	[RequireComponent(typeof(EntityIdentity))]
	public class EntitySaveSystem : MonoBehaviour, IEntitySavePacketProvider {
		[SerializeField] private EntityIdentity identity;
		private readonly Dictionary<Type, ISaveable> _saveableComponents = new();
		private GlobalSaveSystem _globalSaveSystem;

		public HashedTag UniqueID => identity.EntityDetail.UniqueID.HashedTag;

		private void Awake() {
			if (identity == null) identity = GetComponent<EntityIdentity>();
			if (!GlobalServiceLocator.Instance.TryGetService(out this._globalSaveSystem)) {
				Debug.LogError("GlobalSaveSystem not found in the scene. Please ensure it is present for saving functionality to work.");
				return;
			}

			this._globalSaveSystem.RegisterTheEntity(this);
			RegisterSaveDataChunk();
		}

		/// <summary>
		/// Scans the component registry and caches everything that implements ISaveable.
		/// Called during initialization by the identity or global system.
		/// </summary>
		public void RegisterSaveDataChunk() {
			var registry = identity.EntityComponentsRegistryForSaveSystemOnly.ComponentRegistry;
			if (registry == null) return;

			_saveableComponents.Clear();
			var processedObjects = new HashSet<ISaveable>();

			foreach (var kvp in registry.Components) {
				if (kvp.Value is ISaveable saveable && !processedObjects.Contains(saveable)) {
					_saveableComponents[saveable.GetType()] = saveable;
					processedObjects.Add(saveable);
				}
			}
		}

		public EntitySavePacket GetEntitySavePacket() {
			var dataChunks = new Dictionary<Type, ISaveData>();

			foreach (var kvp in _saveableComponents) {
				dataChunks[kvp.Key] = kvp.Value.GetSaveData();
			}
			var ed = identity.EntityDetail;
			return new EntitySavePacket(
				ed.CommonEntityHashedTag,
				ed.EntityIdentityCategoryEnum,
				ed.UniqueID.HashedTag,
				dataChunks
			);
		}

		public void LoadEntitySavePacket(EntitySavePacket packet) {
			var registry = identity.EntityComponentsRegistryForSaveSystemOnly.ComponentRegistry;

			foreach (var kvp in packet.DataSource) {
				Type componentType = kvp.Key;
				ISaveData dataStruct = kvp.Value;

				if (registry.Components.TryGetValue(componentType, out var component)) {
					if (component is ISaveable targetSaveable) {
						targetSaveable.LoadFromSaveData(dataStruct);
					}
				}
			}
		}

		public bool ValidateIdentity(string callerInfo = null) => identity.ValidateIdentity(callerInfo);
	}
}