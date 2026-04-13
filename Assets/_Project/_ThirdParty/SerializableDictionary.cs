using System;
using System.Collections.Generic;
using UnityEngine;

namespace ThirdParty.SerializableDictionary {
	/// <summary>
	/// Serializable dictionary for Unity serialization.    
	/// </summary>
	[Serializable]
	public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver {
		[SerializeField]
		private List<TKey> keys = new();

		[SerializeField]
		private List<TValue> values = new();

		// save the dictionary to lists
		public void OnBeforeSerialize() {
			keys.Clear();
			values.Clear();
			foreach (KeyValuePair<TKey, TValue> pair in this) {
				keys.Add(pair.Key);
				values.Add(pair.Value);
			}
		}

		public void OnAfterDeserialize() {
			this.Clear();
			if (keys.Count != values.Count) {
				Debug.LogWarning($"SerializableDictionary<{typeof(TKey)}, {typeof(TValue)}> mismatch: {keys.Count} keys, {values.Count} values. Clearing...");
				keys = new List<TKey>();
				values = new List<TValue>();
				return;
			}
			for (int i = 0; i < keys.Count; i++)
				this.Add(keys[i], values[i]);
		}

	}
}