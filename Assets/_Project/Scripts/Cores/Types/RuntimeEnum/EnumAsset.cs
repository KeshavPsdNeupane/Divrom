using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Kope.Core.Type.EnumAsset {
	[Serializable]
	public class EnumInstance {
		public string Name;
		public int Value;
		public EnumInstance(string name, int value) {
			Name = name;
			Value = value;
		}
	}

	[CreateAssetMenu(fileName = "NewEnumAsset", menuName = "Kope/Enum Asset")]
	public class EnumAsset : ScriptableObject, ISerializationCallbackReceiver {
		public List<EnumInstance> Instances = new();
		private Dictionary<int, EnumInstance> _cache = new();

		public void AddNewInstance() {
			int nextValue = Instances.Count > 0 ? Instances.Max(i => i.Value) + 1 : 0;
			Instances.Add(new EnumInstance("New Entry", nextValue));
		}

		public EnumInstance GetInstance(int value) {
			return _cache.TryGetValue(value, out var instance) ? instance : null;
		}

		public void OnBeforeSerialize() { }
		public void OnAfterDeserialize() {
			_cache.Clear();
			foreach (var inst in Instances) {
				if (inst != null && !_cache.ContainsKey(inst.Value))
					_cache.Add(inst.Value, inst);
			}
		}
	}
}