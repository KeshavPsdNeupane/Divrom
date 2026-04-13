using System;
using System.Collections.Generic;
using System.Reflection;

namespace Kope.SaveSystem {
	public static class SaveTypeRegistry {
		private static readonly Dictionary<string, Type> FallbackIdToType = new();
		private static readonly Dictionary<Type, string> FallbackTypeToId = new();
		private static bool _hasBuiltFallbackCache;
		private static SaveTypeDatabase _database;

		public static void SetDatabase(SaveTypeDatabase database) {
			_database = database;
		}

		public static bool TryGetId(Type type, out string id) {
			if (type == null) {
				id = null;
				return false;
			}

			if (_database != null && _database.TryGetId(type, out id)) {
				return true;
			}

			EnsureFallbackCache();
			return FallbackTypeToId.TryGetValue(type, out id);
		}

		public static bool TryResolve(string id, out Type type) {
			if (_database != null && _database.TryGetType(id, out type)) {
				return true;
			}

			EnsureFallbackCache();
			return FallbackIdToType.TryGetValue(id, out type);
		}

		public static void ClearFallbackCache() {
			FallbackIdToType.Clear();
			FallbackTypeToId.Clear();
			_hasBuiltFallbackCache = false;
		}

		private static void EnsureFallbackCache() {
			if (_hasBuiltFallbackCache) return;
			_hasBuiltFallbackCache = true;

			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
				Type[] types;
				try {
					types = assembly.GetTypes();
				} catch (ReflectionTypeLoadException exception) {
					types = exception.Types;
				}

				if (types == null) continue;

				foreach (var type in types) {
					if (type == null || type.IsAbstract || type.IsInterface) continue;

					var attribute = type.GetCustomAttribute<SaveIdAttribute>(false);
					if (attribute == null || string.IsNullOrWhiteSpace(attribute.Id)) continue;

					if (!FallbackIdToType.ContainsKey(attribute.Id)) {
						FallbackIdToType[attribute.Id] = type;
					}

					if (!FallbackTypeToId.ContainsKey(type)) {
						FallbackTypeToId[type] = attribute.Id;
					}
				}
			}
		}
	}
}
