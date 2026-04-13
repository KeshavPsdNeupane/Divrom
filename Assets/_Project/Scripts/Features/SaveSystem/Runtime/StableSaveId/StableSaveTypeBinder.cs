using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Kope.SaveSystem {
	public sealed class StableSaveTypeBinder : ISerializationBinder {
		public void BindToName(Type serializedType, out string assemblyName, out string typeName) {
			if (!SaveTypeRegistry.TryGetId(serializedType, out var stableId) || string.IsNullOrWhiteSpace(stableId)) {
				throw new JsonSerializationException($"[StableSaveTypeBinder] No stable SaveId registered for type '{serializedType?.FullName}'.");
			}

			assemblyName = null;
			typeName = stableId;
		}

		public Type BindToType(string assemblyName, string typeName) {
			if (!string.IsNullOrWhiteSpace(typeName) && SaveTypeRegistry.TryResolve(typeName, out var resolvedType)) {
				return resolvedType;
			}

			if (!string.IsNullOrWhiteSpace(typeName)) {
				if (!string.IsNullOrWhiteSpace(assemblyName)) {
					var legacyQualifiedName = $"{typeName}, {assemblyName}";
					var legacyType = Type.GetType(legacyQualifiedName);
					if (legacyType != null) {
						return legacyType;
					}
				}

				var legacyTypeWithoutAssembly = Type.GetType(typeName);
				if (legacyTypeWithoutAssembly != null) {
					return legacyTypeWithoutAssembly;
				}
			}

			throw new JsonSerializationException($"[StableSaveTypeBinder] No type registered for stable SaveId '{typeName}'.");
		}
	}
}