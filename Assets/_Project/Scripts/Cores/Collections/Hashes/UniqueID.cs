using System;
using System.Collections.Generic;
using UnityEngine;
using Kope.Core.Attribute;
using Kope.Core.Collections.Extensions;
using Kope.Core.Collections.Hashes;


#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
namespace Kope.Core.Collections.UniqueId {
	/// <summary>
	/// Always place at the very root of current GameObject since this Id defines the very identity of the entire entity.
	/// If this component is missing, the system will generate a new ID on Awake,
	///  which may cause reference breakage if other entities rely on this ID for 
	/// cross-entity references (e.g., an AI targeting a specific Player ID). 
	/// This component provides a persistent Unique Identifier (GUID) and a high-performance <see cref="HashedTag"/> for GameObjects.
	/// This system ensures uniqueness across scenes and prefabs. It maintains a static registry for O(1) 
	/// lookup of GameObjects by their ID.
	/// <remarks><br/>
	/// Save/Load Implementation: <br/>
	/// Because runtime IDs are stochastic (randomly generated), the Save/Load system cannot rely on the 
	/// engine to recreate the same ID naturally. Instead, the system must:
	/// <br/>1. Serialize the entity's complete state, explicitly including the HashedTag/GUID   .
	/// <br/>2. Upon loading, instantiate the entity and manually re-assign the saved ID to the new instance 
	/// before it registers itself in the global registry.
	/// <br/>This "Identity Injection" ensures that cross-entity reference (e.g., an AI targeting a specific 
	/// Player ID) remain valid across save/load cycles.
	/// </remarks>
	/// </summary>

	[DisallowMultipleComponent]
	public class UniqueID : MonoBehaviour {
		[SerializeField, ReadOnly] private string guid;
		[SerializeField, ReadOnly] private string absoluteHierarchyPath;

		private HashedTag _hashedTag;

		public HashedTag HashedTag {
			get {
				if (string.IsNullOrEmpty(this.guid)) this.GenerateId();
				if (this._hashedTag.ToString() != this.guid) {
					this._hashedTag = new HashedTag(this.guid);
				}
				return this._hashedTag;
			}
		}

		public string Id => this.guid;
		public string Path => this.absoluteHierarchyPath;

		private static readonly Dictionary<HashedTag, GameObject> AllIds = new();

		private void Awake() {
			if (!AllIds.ContainsValue(this.gameObject)) {
				InitializeId();
			}
		}

		/// <summary>
		/// Restores a saved identity. Useful for recreating dynamic entities with their original ID.
		/// </summary>
		public void InjectId(string savedId) {
			if (string.IsNullOrEmpty(savedId)) return;

			if (!string.IsNullOrEmpty(guid)) {
				HashedTag currentTag = HashedTag;
				if (AllIds.TryGetValue(currentTag, out var go) && go == this.gameObject) {
					AllIds.Remove(currentTag);
				}
			}

			this.guid = savedId;
			this._hashedTag = new HashedTag(guid);
			UpdateHierarchyPath(); // Refresh path to match current scene state
			RegisterId();
		}

		private void InitializeId() {
			if (string.IsNullOrEmpty(guid) || IsIdConflict()) {
				GenerateId();
			} else {
				UpdateHierarchyPath();
			}
			RegisterId();
		}

		private bool IsIdConflict() {
			return AllIds.TryGetValue(HashedTag, out var existing) && existing != this.gameObject;
		}

		private void RegisterId() {
			if (!string.IsNullOrEmpty(guid)) {
				AllIds[HashedTag] = this.gameObject;
			}
		}

		private void GenerateId() {
			guid = Guid.NewGuid().ToString();
			_hashedTag = new HashedTag(guid);
			UpdateHierarchyPath();

#if UNITY_EDITOR
			if (!Application.isPlaying) {
				EditorUtility.SetDirty(this);
				if (gameObject.scene.IsValid()) {
					EditorSceneManager.MarkSceneDirty(gameObject.scene);
				}
			}
#endif
		}

		private void UpdateHierarchyPath() {
			absoluteHierarchyPath = BuildHierarchyPath(this.transform);
		}

		private string BuildHierarchyPath(Transform currentTransform) {
			return this.GetGameObjectHierarchyPath();
		}

		public void ResetId() {
			HashedTag current = HashedTag;
			if (AllIds.TryGetValue(current, out var existing) && existing == this.gameObject) {
				AllIds.Remove(current);
			}
			guid = string.Empty;
			InitializeId();
		}

		private void OnDestroy() {
			if (!string.IsNullOrEmpty(guid)) {
				HashedTag current = HashedTag;
				if (AllIds.TryGetValue(current, out var value) && value == this.gameObject) {
					AllIds.Remove(current);
				}
			}
		}

#if UNITY_EDITOR
		private void OnValidate() {
			if (EditorUtility.IsPersistent(this) || PrefabStageUtility.GetCurrentPrefabStage() != null) return;

			// In the editor, always refresh the path in case the object was renamed or moved
			UpdateHierarchyPath();

			if (string.IsNullOrEmpty(guid) || IsIdConflict()) {
				GenerateId();
			}
			RegisterId();
		}
#endif

		public static bool TryGetByTag(HashedTag tag, out GameObject go) {
			return AllIds.TryGetValue(tag, out go);
		}

		public override string ToString() {
			return this.guid;
		}
	}
}