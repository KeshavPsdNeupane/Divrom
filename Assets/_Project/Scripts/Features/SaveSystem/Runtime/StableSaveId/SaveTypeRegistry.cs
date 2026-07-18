using System;
using System.Collections.Generic;
using System.Reflection;
using Kope.SaveSystem.Attributes;
namespace Kope.SaveSystem {
	/// <summary>
	/// Runtime lookup for stable SaveIds. Prefers the <see cref="SaveTypeDatabase"/> asset
	/// if one is registered via <see cref="SetDatabase"/>; otherwise falls back to a
	/// reflection scan of loaded assemblies. The fallback path exists mainly for
	/// editor/test contexts where no database asset has been wired up - it is slower
	/// and should not be relied on in builds.
	/// </summary>
	/// <remarks>
	/// <para><b>Which method do I call?</b> Match your situation below before using anything on this class.</para>
	/// <list type="bullet">
	/// <item>
	/// <description>
	/// Serializing/deserializing an <see cref="ISaveData"/> payload (Newtonsoft type binder):
	/// use <see cref="TryGetDataId"/> / <see cref="TryResolveDataType"/>. This is the only
	/// safe id&lt;-&gt;Type pair for data, since a concrete data type must be reconstructed
	/// FROM the id string alone. Data ids are never shared - there is no InheritSaveId
	/// equivalent for data - so this direction is always unambiguous.
	/// </description>
	/// </item>
	/// <item>
	/// <description>
	/// Writing out a component's save id while packing a save packet: use
	/// <see cref="TryGetComponentId"/>. Safe for any concrete component type, whether it
	/// declares <c>[SaveComponent]</c> directly or picks one up via <c>[InheritSaveId]</c>.
	/// Many types can map to the same id; that's expected and fine in this direction.
	/// </description>
	/// </item>
	/// <item>
	/// <description>
	/// Loading a save packet and needing to find which component on a specific entity a
	/// given save id belongs to: do <b>not</b> use <see cref="TryGetComponentDeclaringType"/>.
	/// Instead, build an id-to-instance map from that entity's own live components (see
	/// <c>EntitySaveSystemBase._saveIdToComponent</c>) by calling <see cref="TryGetComponentId"/>
	/// on each one and inverting locally. A global id-to-Type table cannot answer "which
	/// subclass does this entity have," because <c>[InheritSaveId]</c> lets multiple concrete
	/// types legitimately share one id - the global table only knows the single declaring
	/// ancestor, which is very likely not the concrete type actually present at runtime.
	/// </description>
	/// </item>
	/// <item>
	/// <description>
	/// Building an editor tool or debug inspector that shows "id X belongs to which class" as
	/// a label, with no live instance involved: <see cref="TryGetComponentDeclaringType"/> is
	/// fine here. It answers "which class introduced this id," which is correct for a tooling
	/// label. It is not the concrete type of any particular saved instance - never use its
	/// result to look up a component on an actual entity.
	/// </description>
	/// </item>
	/// </list>
	/// </remarks> 
	public static class SaveTypeRegistry {
		private static readonly Dictionary<string, Type> FallbackDataIdToType = new();
		private static readonly Dictionary<Type, string> FallbackDataTypeToId = new();
		private static readonly Dictionary<Type, string> FallbackComponentTypeToId = new();
		private static readonly Dictionary<string, Type> FallbackComponentIdToDeclaringType = new();

		private static bool _hasBuiltFallbackCache;
		private static SaveTypeDatabase _database;

		public static void SetDatabase(SaveTypeDatabase database) {
			_database = database;
		}

		// ============================================================================
		// DATA - bidirectional, always safe, always unambiguous (data never inherits ids).
		// Used by StableSaveTypeBinder for actual polymorphic ISaveData (de)serialization.
		// ============================================================================

		/// <summary>Type -> id. Use when writing out which concrete ISaveData type this is.</summary>
		public static bool TryGetDataId(Type type, out string id) {
			if (type == null) {
				id = null;
				return false;
			}
			if (_database != null && _database.TryGetDataId(type, out id)) {
				return true;
			}
			EnsureFallbackCache();
			return FallbackDataTypeToId.TryGetValue(type, out id);
		}

		/// <summary>
		/// id -> Type. Use when deserializing: reconstructs the concrete ISaveData
		/// type from its stored id. Safe because data ids are 1:1 by construction.
		/// </summary>
		public static bool TryResolveDataType(string id, out Type type) {
			if (_database != null && _database.TryGetDataType(id, out type)) {
				return true;
			}
			EnsureFallbackCache();
			return FallbackDataIdToType.TryGetValue(id, out type);
		}

		// ============================================================================
		// COMPONENTS - Type -> id is always safe (many-to-one via InheritSaveId is fine).
		// id -> Type only exists for the single DECLARING type and is tooling-only.
		// See the class-level "WHICH METHOD DO I CALL" guide before using either of these
		// for anything touching a live entity.
		// ============================================================================

		/// <summary>
		/// Type -> id. Safe for any concrete component type, whether it declares its
		/// own [SaveComponent] or inherits one via [InheritSaveId]. Use this when
		/// packing a save id for a component you already have an instance of.
		/// </summary>
		public static bool TryGetComponentId(Type type, out string id) {
			if (type == null) {
				id = null;
				return false;
			}
			if (_database != null && _database.TryGetComponentId(type, out id)) {
				return true;
			}
			EnsureFallbackCache();
			return FallbackComponentTypeToId.TryGetValue(type, out id);
		}

		/// <summary>
		/// EDITOR / DEBUG TOOLING ONLY. id -> the single canonical DECLARING type
		/// (the class carrying [SaveComponent] directly) - NOT the concrete runtime
		/// type of any particular saved instance, and NOT safe to use for locating a
		/// component on a live entity during load.
		///
		/// Why: [InheritSaveId] lets many concrete subclasses share one id. This
		/// method can only ever return ONE type per id (the shared ancestor), so if
		/// you use it to look up a component on an entity that actually has a
		/// subclass, the lookup will target the wrong Type and silently miss.
		///
		/// For loading, resolve ids against the entity's own live ISaveable
		/// components instead (see EntitySaveSystemBase) - never against this.
		/// </summary>
		public static bool TryGetComponentDeclaringType(string id, out Type type) {
			if (_database != null && _database.TryGetComponentDeclaringType(id, out type)) {
				return true;
			}
			EnsureFallbackCache();
			return FallbackComponentIdToDeclaringType.TryGetValue(id, out type);
		}

		public static void ClearFallbackCache() {
			FallbackDataIdToType.Clear();
			FallbackDataTypeToId.Clear();
			FallbackComponentTypeToId.Clear();
			FallbackComponentIdToDeclaringType.Clear();
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

					if (typeof(ISaveData).IsAssignableFrom(type)) {
						var dataAttribute = type.GetCustomAttribute<SaveComponentDataAttribute>(inherit: false);
						if (dataAttribute == null || string.IsNullOrWhiteSpace(dataAttribute.Id)) continue;

						if (!FallbackDataIdToType.ContainsKey(dataAttribute.Id)) {
							FallbackDataIdToType[dataAttribute.Id] = type;
						}
						if (!FallbackDataTypeToId.ContainsKey(type)) {
							FallbackDataTypeToId[type] = dataAttribute.Id;
						}
						continue;
					}

					if (typeof(ISaveable).IsAssignableFrom(type)) {
						if (!SaveComponentAttributeResolver.TryGetEffectiveId(type, out var id, out var declaringType)) {
							continue;
						}

						if (!FallbackComponentTypeToId.ContainsKey(type)) {
							FallbackComponentTypeToId[type] = id;
						}
						if (type == declaringType && !FallbackComponentIdToDeclaringType.ContainsKey(id)) {
							FallbackComponentIdToDeclaringType[id] = type;
						}
					}
				}
			}
		}
	}
}