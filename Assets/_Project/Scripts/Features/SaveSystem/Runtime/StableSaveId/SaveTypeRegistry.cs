using System;
using System.Collections.Generic;
using System.Reflection;

namespace Kope.SaveSystem {
	public static class SaveTypeRegistry {
		private static readonly Dictionary<string, Type> IdToType = new();

		public static void Register(Type type) {
			if (type == null) return;

			var attribute = type.GetCustomAttribute<SaveIdAttribute>(false);
			if (attribute == null || string.IsNullOrWhiteSpace(attribute.Id)) return;

			IdToType[attribute.Id] = type;
		}

		public static bool TryResolve(string id, out Type type) {
			return IdToType.TryGetValue(id, out type);
		}

		public static Type Resolve(string id) {
			IdToType.TryGetValue(id, out var type);
			return type;
		}

		public static void Clear() {
			IdToType.Clear();
		}
	}
}
