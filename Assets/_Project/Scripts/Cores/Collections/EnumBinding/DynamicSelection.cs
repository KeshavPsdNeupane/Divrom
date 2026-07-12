namespace Kope.Core.Attribute.DataStructure {
	using System;
	using System.Collections.Generic;
	using System.Reflection;
	using UnityEngine;

	/// <summary>
	/// A polymorphic container that dynamically binds an enum selection to a specific data field using <see cref="BindToEnumAttribute"/>.
	/// <para>
	/// This enables a "Swapping UI" in the Unity Inspector that avoids the fragility of Managed References ([SerializeReference]) 
	/// while maintaining a clean, type-safe API for retrieving concrete logic.
	/// </para>
	/// </summary>
	/// <remarks>
	/// <para><b>CRITICAL:</b> The enumeration <typeparamref name="TEnum"/> MUST define its safest fallback/default behavior 
	/// at index 0. This ensures uninitialized components default to a predictable state (e.g., Self-Targeting).</para>
	/// 
	/// <para><b>AUTO-INSTANTIATION:</b> If a bound field is null, <see cref="GetSelected"/> attempts to 
	/// instantiate the <c>TargetType</c> defined in the attribute. This allows for lazy initialization of data structures.</para>
	/// 
	/// <para><b>PERFORMANCE &amp; LISTS:</b> While this class performs internal instance-level caching, 
	/// reflection is still required to find the bound field on the first access (a "Cache Miss"). 
	/// If using a <c>List&lt;DynamicSelection&gt;</c>, it is highly recommended to "warm" the cache 
	/// by calling <see cref="GetSelected"/> for each element during initialization (e.g., in <c>OnEnable</c>) 
	/// to prevent frame spikes during high-frequency execution like <c>Ability.Execute()</c>.</para>
	/// </remarks>
	/// <typeparam name="TEnum">The enumeration type used for selection.</typeparam>
	/// <typeparam name="TBase">The base interface or class type the selected fields implement.</typeparam>
	[Serializable]
	public abstract class DynamicSelection<TEnum, TBase> where TEnum : Enum {
		public TEnum selectedType;

		// Keyed by (concrete subclass type, enum value) since different subclasses
		// have different field layouts. Null is a valid cached result (missing binding).
		private static readonly Dictionary<(Type, object), (FieldInfo field, Type targetType)> _fieldCache = new();
		/// <summary>
		/// Caches the selected field's value after the first retrieval to optimize subsequent accesses.
		/// This assumes that the selected enum value does not change at runtime. If the enum selection can change,
		/// this cache should be invalidated accordingly (not implemented in this version for simplicity).
		/// </summary>
		private TBase _cachedEnumType;
		private TEnum _lastSelectedType;


		/// <summary>
		/// Retrieves the instance of <typeparamref name="TBase"/> associated with the currently selected 
		/// <typeparamref name="TEnum"/> value.<br/>
		/// On the first call, it uses reflection to find the field bound to the selected enum value, 
		/// retrieves its value, and caches it for future calls.<br/>
		/// If the bound field's value is null and the field's type is concrete, it will attempt to 
		/// auto-instantiate it using the TargetType specified in the <see cref="BindToEnumAttribute"/>.
		/// This allows for lazy initialization of the bound data, but be cautious as it will create
		/// an instance on the fly if accessed without proper setup.
		/// </summary>
		/// <returns></returns>
		public TBase GetSelected() {
			// highly optimized path for repeated access without changing the selected enum value,
			//  which is the common case in ability execution
			if (this._cachedEnumType != null && this._lastSelectedType.Equals(this.selectedType)) {
				return this._cachedEnumType;
			}
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
				value = Activator.CreateInstance(cached.targetType);
				cached.field.SetValue(this, value);
			}
			this._cachedEnumType = (TBase)value;
			this._lastSelectedType = this.selectedType;
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