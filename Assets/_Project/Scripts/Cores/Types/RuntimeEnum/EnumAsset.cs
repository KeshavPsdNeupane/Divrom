using System;
using System.Collections.Generic;
using ZLinq;
using UnityEngine;

namespace Kope.Core.Type.EnumAsset {
	[Serializable]
	public sealed class EnumInstance {
		/// <summary>
		/// HUMAN-READABLE IDENTIFIER
		/// Used for Animator hashing and debugging.
		/// </summary>
		public string Alias => this._name;

		/// <summary>
		/// PERSISTENT DATA HANDLE
		/// Used for save files and O(1) dictionary lookups.
		/// </summary>
		public int InternalValue => _value;

		// ==========================================================================================
		// CRITICAL: DO NOT RENAME '_name' OR '_value'.
		// 1. REFLECTION: The custom 'EnumAssetEditor' targets these specific field names.
		// 2. SERIALIZATION: Renaming these will break all existing ScriptableObject (.asset) data.
		// 3. OPTIMIZATION: int32 is used over smaller types (byte/short) to maintain alignment
		//    simplicity, as entry counts are typically low enough that memory gains are negligible.
		// ==========================================================================================

		[SerializeField] private string _name;
		[SerializeField] private int _value;

		public EnumInstance(string name, int value) {
			this._name = name;
			this._value = value;
		}

		public override string ToString() => this._name;
	}




	[CreateAssetMenu(fileName = "NewEnumAsset", menuName = "Kope/Enum Asset")]
	public class EnumAsset : ScriptableObject, ISerializationCallbackReceiver {
		public List<EnumInstance> Instances = new();
		private readonly Dictionary<int, EnumInstance> _cache = new();

		public void AddNewInstance() {
			int nextValue = Instances.Count > 0 ? Instances.AsValueEnumerable().Max(i => i.InternalValue) + 1 : 0;
			Instances.Add(new EnumInstance("New Entry", nextValue));
		}

		public EnumInstance GetInstance(int value) {
			return this._cache.TryGetValue(value, out var instance) ? instance : null;
		}

		public void OnBeforeSerialize() { }
		public void OnAfterDeserialize() {
			this._cache.Clear();
			foreach (var inst in Instances) {
				if (inst != null && !this._cache.ContainsKey(inst.InternalValue))
					this._cache.Add(inst.InternalValue, inst);
			}
		}
	}
}