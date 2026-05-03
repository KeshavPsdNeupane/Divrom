using System;
using System.Collections.Generic;
using ZLinq;
using UnityEngine;

namespace Kope.Core.Type.EnumAsset {
	[Serializable]
	public class EnumInstance {
		// i dont think i need to do and optimize on the level of 
		// value size like  short, byte, etc, since the main use case for this is for 
		// designer-friendly enums, and they usually dont have a huge amount of entries
		public string Name;
		public int Value;
		public EnumInstance(string name, int value) {
			Name = name;
			Value = value;
		}
		public override string ToString() => $"{Name}";
	}



	[CreateAssetMenu(fileName = "NewEnumAsset", menuName = "Kope/Enum Asset")]
	public class EnumAsset : ScriptableObject, ISerializationCallbackReceiver {
		public List<EnumInstance> Instances = new();
		private Dictionary<int, EnumInstance> _cache = new();

		public void AddNewInstance() {
			int nextValue = Instances.Count > 0 ? Instances.AsValueEnumerable().Max(i => i.Value) + 1 : 0;
			Instances.Add(new EnumInstance("New Entry", nextValue));
		}

		public EnumInstance GetInstance(int value) {
			return _cache.TryGetValue(value, out var instance) ? instance : null;
		}

		public void OnBeforeSerialize() { }
		public void OnAfterDeserialize() {
			this._cache.Clear();
			foreach (var inst in Instances) {
				if (inst != null && !this._cache.ContainsKey(inst.Value))
					this._cache.Add(inst.Value, inst);
			}
		}
	}
}