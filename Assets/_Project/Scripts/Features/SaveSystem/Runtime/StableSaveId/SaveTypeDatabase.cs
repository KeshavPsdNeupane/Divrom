using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kope.SaveSystem {
	[CreateAssetMenu(menuName = "Kope/Save System/Save Type Database", fileName = "SaveTypeDatabase")]
	public sealed class SaveTypeDatabase : ScriptableObject {
		[SerializeField] private List<SaveTypeEntry> entries = new();

		[NonSerialized] private Dictionary<string, Type> _idToType;
		[NonSerialized] private Dictionary<Type, string> _typeToId;

		private void OnEnable() {
			RebuildRuntimeCache();
		}

		private void OnValidate() {
			RebuildRuntimeCache();
		}

		public void RebuildRuntimeCache() {
			_idToType = new Dictionary<string, Type>(StringComparer.Ordinal);
			_typeToId = new Dictionary<Type, string>();

			foreach (var entry in this.entries) {
				if (entry == null || string.IsNullOrWhiteSpace(entry.Id)) continue;

				var resolvedType = entry.ResolveType();
				if (resolvedType == null) continue;

				_idToType[entry.Id] = resolvedType;
				_typeToId[resolvedType] = entry.Id;
			}
		}

		public bool TryGetType(string id, out Type type) {
			if (_idToType == null) RebuildRuntimeCache();
			return _idToType.TryGetValue(id, out type);
		}

		public bool TryGetId(Type type, out string id) {
			if (_typeToId == null) RebuildRuntimeCache();
			return _typeToId.TryGetValue(type, out id);
		}

#if UNITY_EDITOR
		public void RebuildFromProject() {
			var foundEntries = new List<SaveTypeEntry>();
			var duplicateIds = new HashSet<string>(StringComparer.Ordinal);

			foreach (var type in UnityEditor.TypeCache.GetTypesWithAttribute<SaveIdAttribute>()) {
				if (type == null || type.IsAbstract || type.IsInterface) continue;

				if (!typeof(ISaveable).IsAssignableFrom(type) && !typeof(ISaveData).IsAssignableFrom(type)) continue;

				if (Attribute.GetCustomAttribute(type, typeof(SaveIdAttribute), false) is not SaveIdAttribute attribute || string.IsNullOrWhiteSpace(attribute.Id)) continue;

				if (!duplicateIds.Add(attribute.Id)) {
					Debug.LogError($"[SaveTypeDatabase] Duplicate SaveId '{attribute.Id}' found. Keep IDs unique.");
					continue;
				}

				foundEntries.Add(new SaveTypeEntry {
					Id = attribute.Id,
					AssemblyQualifiedTypeName = type.AssemblyQualifiedName,
					DisplayName = type.FullName
				});
			}

			foundEntries.Sort((left, right) => string.Compare(left.Id, right.Id, StringComparison.Ordinal));
			this.entries = foundEntries;
			UnityEditor.EditorUtility.SetDirty(this);
			RebuildRuntimeCache();
		}
#endif

		[Serializable]
		public sealed class SaveTypeEntry {
			[SerializeField] public string Id;
			[SerializeField] public string AssemblyQualifiedTypeName;
			[SerializeField] public string DisplayName;

			public Type ResolveType() {
				if (string.IsNullOrWhiteSpace(this.AssemblyQualifiedTypeName)) return null;
				return Type.GetType(this.AssemblyQualifiedTypeName);
			}
		}
	}
}