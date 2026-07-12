using System.Collections.Generic;
using Kope.Core.Collections.Hashes;
using UnityEngine;

namespace Kope.Core.EntityComponentRegistry {
	[CreateAssetMenu(fileName = "EntityCommonNameConfig", menuName = "Scriptable Objects/Actors/EntityCommonNameConfig", order = 1)]
	public class EntityCommonNameConfig : ScriptableObject {
		[SerializeField, Tooltip("List of all valid common entity names used across the system.")]
		private List<string> commonEntityNames = new();
		private Dictionary<HashedTag, string> commonNameToHashedTagMap;

		private void OnEnable() => InitHash();
		private void OnValidate() => InitHash();

		private void InitHash() {
			if (this.commonNameToHashedTagMap == null) this.commonNameToHashedTagMap = new Dictionary<HashedTag, string>();
			else this.commonNameToHashedTagMap.Clear();

			foreach (var nameEntry in this.commonEntityNames) {
				if (string.IsNullOrEmpty(nameEntry)) continue;

				var hashedTag = new HashedTag(nameEntry);
				if (!this.commonNameToHashedTagMap.ContainsKey(hashedTag)) {
					this.commonNameToHashedTagMap.Add(hashedTag, nameEntry);
				} else {
					Debug.LogWarning($"[{this.name}] Duplicate common entity name: '{nameEntry}'.");
				}
			}
		}

		public bool InternalContains(HashedTag hashedTag) {
			// Ensure map is ready if InternalContains is called before OnEnable/Validate
			if (this.commonNameToHashedTagMap == null) InitHash();
			return this.commonNameToHashedTagMap.ContainsKey(hashedTag);
		}

	}
}
