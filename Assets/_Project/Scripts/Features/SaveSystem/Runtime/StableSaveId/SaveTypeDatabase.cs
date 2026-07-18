using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Kope.SaveSystem.Attributes;
namespace Kope.SaveSystem {
	[CreateAssetMenu(menuName = "Kope/Save System/Save Type Database", fileName = "SaveTypeDatabase")]
	public sealed class SaveTypeDatabase : ScriptableObject {
		[SerializeField] private List<SaveDataTypeEntry> dataEntries = new();
		[SerializeField] private List<SaveComponentTypeEntry> componentEntries = new();

		// Data: fully bidirectional. Data types never inherit their id, so id -> Type is always unambiguous.
		[NonSerialized] private Dictionary<string, Type> _dataIdToType;
		[NonSerialized] private Dictionary<Type, string> _dataTypeToId;




		// Components: Type -> id always populated (root + every inheritor).
		// id -> Type only populated for the single declaring type of that id - never ambiguous,
		// because components are located via hierarchy/ECS path at load time, not reconstructed from id.
		[NonSerialized] private Dictionary<Type, string> _componentTypeToId;
		[NonSerialized] private Dictionary<string, Type> _componentIdToDeclaringType;

		private void OnEnable() => RebuildRuntimeCache();
		private void OnValidate() => RebuildRuntimeCache();

		public void RebuildRuntimeCache() {
			this._dataIdToType = new Dictionary<string, Type>(StringComparer.Ordinal);
			this._dataTypeToId = new Dictionary<Type, string>();
			foreach (var entry in this.dataEntries) {
				if (entry == null || string.IsNullOrWhiteSpace(entry.Id)) continue;
				var resolvedType = entry.ResolveType();
				if (resolvedType == null) continue;
				this._dataIdToType[entry.Id] = resolvedType;
				this._dataTypeToId[resolvedType] = entry.Id;
			}

			this._componentTypeToId = new Dictionary<Type, string>();
			this._componentIdToDeclaringType = new Dictionary<string, Type>(StringComparer.Ordinal);
			foreach (var entry in this.componentEntries) {
				if (entry == null || string.IsNullOrWhiteSpace(entry.Id)) continue;
				var resolvedType = entry.ResolveType();
				if (resolvedType == null) continue;

				this._componentTypeToId[resolvedType] = entry.Id;
				if (entry.IsDeclaringType) {
					this._componentIdToDeclaringType[entry.Id] = resolvedType;
				}
			}
		}

		// ---- Data lookups (bidirectional) ----

		public bool TryGetDataType(string id, out Type type) {
			if (this._dataIdToType == null) RebuildRuntimeCache();
			return this._dataIdToType.TryGetValue(id, out type);
		}

		public bool TryGetDataId(Type type, out string id) {
			if (this._dataTypeToId == null) RebuildRuntimeCache();
			return this._dataTypeToId.TryGetValue(type, out id);
		}

		// ---- Component lookups (Type -> id always; id -> Type only for the declaring type) ----

		public bool TryGetComponentId(Type type, out string id) {
			if (this._componentTypeToId == null) RebuildRuntimeCache();
			return this._componentTypeToId.TryGetValue(type, out id);
		}

		public bool TryGetComponentDeclaringType(string id, out Type type) {
			if (this._componentIdToDeclaringType == null) RebuildRuntimeCache();
			return this._componentIdToDeclaringType.TryGetValue(id, out type);
		}

#if UNITY_EDITOR
		public void RebuildFromProject() {
			var newDataEntries = new List<SaveDataTypeEntry>();
			var seenDataIds = new HashSet<string>(StringComparer.Ordinal);

			foreach (var type in UnityEditor.TypeCache.GetTypesWithAttribute<SaveComponentDataAttribute>()) {
				if (type == null || type.IsAbstract || type.IsInterface) continue;
				if (!typeof(ISaveData).IsAssignableFrom(type)) continue;

				var attribute = type.GetCustomAttribute<SaveComponentDataAttribute>(inherit: false);
				if (attribute == null || string.IsNullOrWhiteSpace(attribute.Id)) continue;

				if (!seenDataIds.Add(attribute.Id)) {
					Debug.LogError($"[SaveTypeDatabase] Duplicate SaveComponentData id '{attribute.Id}' - " +
						$"'{type.FullName}' collides with a type already claiming this id. " +
						"Data ids can never be shared; give one of these a unique id.");
					continue;
				}

				newDataEntries.Add(new SaveDataTypeEntry {
					Id = attribute.Id,
					AssemblyQualifiedTypeName = type.AssemblyQualifiedName,
					DisplayName = type.FullName
				});
			}

			var newComponentEntries = new List<SaveComponentTypeEntry>();
			var seenDeclaringIds = new HashSet<string>(StringComparer.Ordinal);

			foreach (var type in UnityEditor.TypeCache.GetTypesDerivedFrom<ISaveable>()) {
				if (type == null || type.IsAbstract || type.IsInterface) continue;

				bool hasInheritAttribute = type.GetCustomAttribute<InheritSaveIdAttribute>(inherit: false) != null;
				bool resolved = SaveComponentAttributeResolver.TryGetEffectiveId(
					type, out var id, out var declaringType, out var failureReason);

				if (!resolved) {
					// DEBUG SUGGESTION: distinguish "not tagged at all" (expected, silent - most
					// ISaveable-derived types won't opt into the save-id system) from "explicitly
					// tagged with InheritSaveId but resolution failed" (almost always a mistake -
					// wrong SearchNParent, or the ancestor chain was refactored and the intended
					// SaveComponent-declaring base is now further away than the configured depth).
					if (hasInheritAttribute && failureReason == SaveComponentAttributeResolver.ResolutionFailureReason.InheritDepthExceeded) {
						Debug.LogWarning($"[SaveTypeDatabase] '{type.FullName}' has [InheritSaveId] but no " +
							"SaveComponent-declaring ancestor was found within its configured SearchNParent. " +
							"It will NOT be save-tracked. Increase SearchNParent or check the class hierarchy.");
					}
					continue;
				}

				bool isDeclaringType = type == declaringType;
				if (isDeclaringType && !seenDeclaringIds.Add(id)) {
					Debug.LogError($"[SaveTypeDatabase] Duplicate SaveComponent id '{id}' declared directly " +
						$"on '{type.FullName}'. Each id may only be declared once via [SaveComponent]; " +
						"use [InheritSaveId] on subclasses instead of redeclaring the same id.");
					continue;
				}

				newComponentEntries.Add(new SaveComponentTypeEntry {
					Id = id,
					AssemblyQualifiedTypeName = type.AssemblyQualifiedName,
					DisplayName = type.FullName,
					IsDeclaringType = isDeclaringType
				});
			}

			newDataEntries.Sort((l, r) => string.Compare(l.Id, r.Id, StringComparison.Ordinal));
			newComponentEntries.Sort((l, r) => string.Compare(l.DisplayName, r.DisplayName, StringComparison.Ordinal));

			this.dataEntries = newDataEntries;
			this.componentEntries = newComponentEntries;
			UnityEditor.EditorUtility.SetDirty(this);
			RebuildRuntimeCache();
		}
#endif

		[Serializable]
		public sealed class SaveDataTypeEntry {
			[SerializeField] public string Id;
			[SerializeField] public string AssemblyQualifiedTypeName;
			[SerializeField] public string DisplayName;

			public Type ResolveType() {
				if (string.IsNullOrWhiteSpace(this.AssemblyQualifiedTypeName)) return null;
				return Type.GetType(this.AssemblyQualifiedTypeName);
			}
		}

		[Serializable]
		public sealed class SaveComponentTypeEntry {
			[SerializeField] public string Id;
			[SerializeField] public string AssemblyQualifiedTypeName;
			[SerializeField] public string DisplayName;
			[SerializeField] public bool IsDeclaringType;

			public Type ResolveType() {
				if (string.IsNullOrWhiteSpace(this.AssemblyQualifiedTypeName)) return null;
				return Type.GetType(this.AssemblyQualifiedTypeName);
			}
		}
	}
}