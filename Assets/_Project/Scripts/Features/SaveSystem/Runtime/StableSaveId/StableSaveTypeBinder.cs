using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Kope.SaveSystem {
	/// <summary>
	/// Newtonsoft type binder for polymorphic ISaveData fields. Maps concrete data types
	/// to their stable [SaveComponentData] id and back. This binder must never be used for
	/// ISaveable/component types - those are located via hierarchy/ECS path at load time,
	/// not reconstructed from a serialized type name, and have no id -> Type mapping at all.
	/// </summary>
	public sealed class StableSaveTypeBinder : ISerializationBinder {
		public void BindToName(Type serializedType, out string assemblyName, out string typeName) {
			if (serializedType != null && !typeof(ISaveData).IsAssignableFrom(serializedType)) {
				// DEBUG SUGGESTION: catches a misrouted component type at the point of
				// serialization, with the actual offending type name, instead of failing
				// later with a generic "no stable SaveId registered" that doesn't explain
				// why - components never get a data id, so TryGetDataId would just return
				// false and produce a confusing error otherwise.
				throw new JsonSerializationException(
					$"[StableSaveTypeBinder] '{serializedType.FullName}' is not an ISaveData type. " +
					"This binder only serializes ISaveData payloads; ISaveable/component types are " +
					"located via hierarchy/ECS path and should never be passed through it.");
			}

			if (!SaveTypeRegistry.TryGetDataId(serializedType, out var stableId) || string.IsNullOrWhiteSpace(stableId)) {
				throw new JsonSerializationException(
					$"[StableSaveTypeBinder] No stable SaveComponentData id registered for type " +
					$"'{serializedType?.FullName}'. Add a [SaveComponentData(\"...\")] attribute to it.");
			}

			assemblyName = null;
			typeName = stableId;
		}

		public Type BindToType(string assemblyName, string typeName) {
			if (!string.IsNullOrWhiteSpace(typeName) && SaveTypeRegistry.TryResolveDataType(typeName, out var resolvedType)) {
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

			throw new JsonSerializationException(
				$"[StableSaveTypeBinder] No type registered for stable SaveComponentData id '{typeName}'. " +
				"Either the id was renamed/removed, or this save file predates a refactor and needs migration.");
		}
	}
}