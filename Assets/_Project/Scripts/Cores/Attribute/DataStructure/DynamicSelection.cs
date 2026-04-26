namespace Kope.Core.Attribute.DataStructure {
	using System;
	using System.Collections.Generic;
	using System.Reflection;
	using UnityEngine;

	/// <summary>
	/// Provides a polymorphic container that dynamically binds an enum selection to a specific data field.<br/>
	/// This system uses <see cref="BindToEnumAttribute"/> to link enum values to concrete class fields, 
	/// enabling a "Swapping UI" in the Inspector without the fragility of Managed References.<br/><br/>
	/// <b>CRITICAL:</b> The enumeration <typeparamref name="TEnum"/> MUST define its default behavior 
	/// (the safest fallback) at index 0. This ensures that uninitialized components default to a 
	/// predictable state (e.g., Self-Targeting) rather than an invalid one.<br/><br/>
	/// <b>Note:</b> If a bound field is null, <see cref="GetSelected"/> will attempt to 
	/// auto-instantiate the type defined in the attribute's TargetType.
	/// </summary>
	/// <typeparam name="TEnum">The enumeration type used for selection.</typeparam>
	/// <typeparam name="TBase">The base interface or class type the selected fields implement.</typeparam>

	[Serializable]
	public abstract class DynamicSelection<TEnum, TBase> where TEnum : Enum {
		public TEnum selectedType;

		// Keyed by (concrete subclass type, enum value) since different subclasses
		// have different field layouts. Null is a valid cached result (missing binding).
		private static readonly Dictionary<(Type, object), (FieldInfo field, Type targetType)> _fieldCache = new();

		public TBase GetSelected() {
			var key = (this.GetType(), (object)this.selectedType);

			if (!_fieldCache.TryGetValue(key, out var cached)) {
				cached = FindBoundField(GetType(), this.selectedType);
				_fieldCache[key] = cached;
			}

			if (cached.field == null) {
#if UNITY_EDITOR
				// this null will never happen since all field is bound to a enum otherwise they will
				// fallback to the default value which is index 0, and index 0 must be bound to a field,
				// so if this happen it means the developer forget to bind a field to the default enum value
				Debug.LogError(
					$"[DynamicSelection] No field bound to enum value '{this.selectedType}' " +
					$"on type '{this.GetType().Name}'. Did you forget [BindToEnum]?"
				);
#endif
				return default;
			}

			var value = cached.field.GetValue(this);
			if (cached.targetType.IsAbstract || cached.targetType.IsInterface) {
#if UNITY_EDITOR
				Debug.LogError(
					$"[DynamicSelection] Cannot auto-instantiate abstract/interface type '{cached.targetType.Name}' " +
					$"for enum value '{this.selectedType}' on type '{this.GetType().Name}'."
				);
#endif
				return default;
			}
			// this should handle both ScriptableObject and regular class types, since 
			// some of the data is better represented as SO (e.g., damage formula) while
			// some is better as plain class (e.g., targeting logic)
			if (value == null && cached.targetType != null) {
				if (typeof(ScriptableObject).IsAssignableFrom(cached.targetType)) {
					value = ScriptableObject.CreateInstance(cached.targetType);
				} else {
					value = Activator.CreateInstance(cached.targetType);
				}
				cached.field.SetValue(this, value);
			}

			return (TBase)value;
		}

		private static (FieldInfo field, Type targetType) FindBoundField(Type containerType, TEnum enumValue) {
			var fields = containerType.GetFields(
				BindingFlags.NonPublic |
				BindingFlags.Public |
				BindingFlags.Instance
			);

			foreach (var field in fields) {
				var attr = (BindToEnumAttribute)Attribute.GetCustomAttribute(field, typeof(BindToEnumAttribute));
				if (attr != null && attr.EnumValue.Equals(enumValue)) {
					return (field, attr.TargetType);
				}
			}

			return (null, null);
		}
	}
}