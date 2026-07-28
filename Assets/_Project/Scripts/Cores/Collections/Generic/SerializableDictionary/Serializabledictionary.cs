using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Kope.Core.Collections {
	/// <summary>
	/// A Dictionary&lt;TKey, TValue&gt; Unity can actually serialize and edit in the Inspector.
	/// Uses a dirty flag setup to ensure in-progress Inspector duplicates aren't instantly wiped.
	/// </summary>
	[Serializable]
	public class SerializableDictionary<TKey, TValue> :
		IDictionary<TKey, TValue>,
		IReadOnlyDictionary<TKey, TValue>,
		ISerializationCallbackReceiver {

		[SerializeField] private List<TKey> keys = new();
		[SerializeField] private List<TValue> values = new();

		private readonly Dictionary<TKey, TValue> _dict = new();

		// Tracks if the dictionary was modified via C# API code.
		// If false, we skip OnBeforeSerialize to let Inspector duplicates survive.
		private bool _isDirty;

		/// <summary>
		/// Keys that were rejected on the last OnAfterDeserialize because they were null or
		/// a duplicate of an earlier key.
		/// </summary>
		[NonSerialized] public readonly List<TKey> ConflictedKeys = new();

		public SerializableDictionary() { }

		// Optimization Note: Before, _dict's field initializer ("= new()") always used
		// EqualityComparer<TKey>.Default, so any SerializableDictionary<Vector2Int,_> was locked
		// into Vector2Int's default hash — which collides badly on grid-adjacent coordinates (see
		// Vector2IntComparer.cs). There was no constructor path to override it. Now, an optional
		// comparer can be supplied and is forwarded straight into the backing Dictionary's own
		// constructor, exactly like System.Collections.Generic.Dictionary allows. Omitting it (or
		// passing null) reproduces the exact previous behavior, so this is purely additive and
		// doesn't change serialization or any existing call site.
		public SerializableDictionary(IEqualityComparer<TKey> comparer) {
			this._dict = new Dictionary<TKey, TValue>(comparer);
		}

		public SerializableDictionary(IDictionary<TKey, TValue> source, IEqualityComparer<TKey> comparer = null) {
			this._dict = new Dictionary<TKey, TValue>(comparer);
			foreach (KeyValuePair<TKey, TValue> kvp in source)
				this._dict.Add(kvp.Key, kvp.Value);
			this._isDirty = true;
		}

		#region IDictionary<TKey, TValue>

		public TValue this[TKey key] {
			get => this._dict[key];
			set {
				this._dict[key] = value;
				this._isDirty = true;
			}
		}

		public ICollection<TKey> Keys => this._dict.Keys;
		public ICollection<TValue> Values => this._dict.Values;
		public int Count => this._dict.Count;
		public bool IsReadOnly => false;

		IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => this._dict.Keys;
		IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => this._dict.Values;

		public void Add(TKey key, TValue value) {
			this._dict.Add(key, value);
			this._isDirty = true;
		}

		public void Add(KeyValuePair<TKey, TValue> item) {
			this._dict.Add(item.Key, item.Value);
			this._isDirty = true;
		}

		public bool TryAdd(TKey key, TValue value) {
			if (this._dict.ContainsKey(key))
				return false;
			this._dict.Add(key, value);
			this._isDirty = true;
			return true;
		}

		public void Clear() {
			this._dict.Clear();
			this._isDirty = true;
		}

		public void PrintFirstNEntries(int n, string label) {
			int count = 0;
			if (n <= 0) {
				Debug.LogWarning("Requested to print zero or negative number of entries. No output will be shown.");
				return;
			}
			if (this._dict.Count == 0) {
				Debug.LogWarning("The dictionary is empty. No entries to print.");
				return;
			}
			StringBuilder sb = new StringBuilder();
			sb.AppendLine($"Printing first {n} entries of SerializableDictionary<{typeof(TKey).Name}, {typeof(TValue).Name}> ({label}):");
			foreach (var kvp in this._dict) {
				sb.AppendLine($"Key: {kvp.Key}, Value: {kvp.Value}");
				count++;
				if (count >= n) break;
			}
			Debug.Log(sb.ToString());
		}

		public bool Contains(KeyValuePair<TKey, TValue> item) =>
			this._dict.TryGetValue(item.Key, out TValue value) && EqualityComparer<TValue>.Default.Equals(value, item.Value);

		public bool ContainsKey(TKey key) => this._dict.ContainsKey(key);

		public bool ContainsValue(TValue value) => this._dict.ContainsValue(value);

		public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) =>
			((ICollection<KeyValuePair<TKey, TValue>>)this._dict).CopyTo(array, arrayIndex);

		public bool Remove(TKey key) {
			bool removed = this._dict.Remove(key);
			if (removed) this._isDirty = true;
			return removed;
		}

		public bool Remove(KeyValuePair<TKey, TValue> item) {
			if (Contains(item) && this._dict.Remove(item.Key)) {
				this._isDirty = true;
				return true;
			}
			return false;
		}

		public bool TryGetValue(TKey key, out TValue value) => this._dict.TryGetValue(key, out value);

		public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => this._dict.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		#endregion

		#region ISerializationCallbackReceiver

		public void OnBeforeSerialize() {
			// If the dictionary wasn't changed via code, don't overwrite the serialized arrays.
			// This stops Unity from wiping duplicate entries out from under the user in the Inspector.
			if (!this._isDirty) return;
			this._isDirty = false;

			this.keys.Clear();
			this.values.Clear();
			this.keys.Capacity = this.values.Capacity = this._dict.Count;
			foreach (KeyValuePair<TKey, TValue> kvp in this._dict) {
				this.keys.Add(kvp.Key);
				this.values.Add(kvp.Value);
			}
		}
		public void OnAfterDeserialize() {
			this._dict.Clear();
			this.ConflictedKeys.Clear();

			int count = Mathf.Min(this.keys.Count, this.values.Count);

			if (this.keys.Count != this.values.Count) {
				Debug.LogWarning(
					$"SerializableDictionary<{typeof(TKey).Name}, {typeof(TValue).Name}> key/value " +
					$"count mismatch ({this.keys.Count} keys, {this.values.Count} values). Using the first " +
					$"{count} pairs; the rest are discarded this load.");
			}

			for (int i = 0; i < count; i++) {
				TKey key = this.keys[i];
				if (key == null || this._dict.ContainsKey(key)) {
					this.ConflictedKeys.Add(key);
					continue;
				}
				this._dict.Add(key, this.values[i]);
			}
		}
		#endregion
	}
}