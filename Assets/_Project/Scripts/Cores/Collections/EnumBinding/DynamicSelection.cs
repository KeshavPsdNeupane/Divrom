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
	/// at index 0. This ensures uninitialized components default to a predictable state.</para>
	/// 
	/// <para><b>AUTO-INSTANTIATION:</b> If a bound field is null, <see cref="GetSelected"/> attempts to 
	/// instantiate the <c>TargetType</c> defined in the attribute using a cached factory delegate.</para>
	/// 
	/// <para><b>PERFORMANCE:</b> To prevent frame spikes during high-frequency execution, this class uses 
	/// cached reflection and compiled factory delegates. It is recommended to "warm" the cache 
	/// by calling <see cref="GetSelected"/> during initialization (e.g., in <c>OnEnable</c>).</para>
	/// </remarks>
	/// <typeparam name="TEnum">The enumeration type used for selection.</typeparam>
	/// <typeparam name="TBase">The base interface or class type the selected fields implement.</typeparam>
	[Serializable]
	public abstract class DynamicSelection<TEnum, TBase> where TEnum : Enum {
		public TEnum selectedType;

		// Stores the field, the target type, and a compiled factory delegate to bypass slow Activator calls.
		private static readonly Dictionary<(Type, object), (FieldInfo field, Type targetType, Func<object> creator)> _fieldCache = new();

		private TBase _cachedEnumType;
		private TEnum _lastSelectedType;

		/// <summary>
		/// Retrieves the instance of <typeparamref name="TBase"/> associated with the currently selected 
		/// <typeparamref name="TEnum"/> value.
		/// <para>Uses internal caching to ensure that retrieval is near-zero cost after the first access.</para>
		/// </summary>
		public TBase GetSelected() {
			// Optimized path for repeated access without changing the enum selection
			if (this._cachedEnumType != null && this._lastSelectedType.Equals(this.selectedType)) {
				return this._cachedEnumType;
			}

			var key = (this.GetType(), (object)this.selectedType);

			if (!_fieldCache.TryGetValue(key, out var cached)) {
				var (field, targetType) = FindBoundField(GetType(), this.selectedType);

				if (field == null) {
#if UNITY_EDITOR
					Debug.LogError($"[DynamicSelection] No field bound to enum value '{this.selectedType}' on type '{this.GetType().Name}'. Did you forget [BindToEnum]?");
#endif
					return default;
				}

				// Cache the creation logic as a delegate to avoid reflection overhead during runtime
				object creator() => Activator.CreateInstance(targetType);
				cached = (field, targetType, creator);
				_fieldCache[key] = cached;
			}

			var value = cached.field.GetValue(this);

			// Auto-instantiate if null using the cached factory delegate
			if (value == null && cached.targetType != null) {
				if (cached.targetType.IsAbstract || cached.targetType.IsInterface) {
#if UNITY_EDITOR
					Debug.LogError($"[DynamicSelection] Cannot auto-instantiate abstract/interface type '{cached.targetType.Name}' for enum '{this.selectedType}'.");
#endif
					return default;
				}

				value = cached.creator();
				cached.field.SetValue(this, value);
			}

			this._cachedEnumType = (TBase)value;
			this._lastSelectedType = this.selectedType;
			return (TBase)value;
		}

		/// <summary>
		/// Searches for the field decorated with <see cref="BindToEnumAttribute"/> matching the selected enum.
		/// </summary>
		private static (FieldInfo field, Type targetType) FindBoundField(Type containerType, TEnum enumValue) {
			var fields = containerType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

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