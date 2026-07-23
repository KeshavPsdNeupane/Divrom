using UnityEngine;
namespace Kope.Core.Type.Generic {

	[System.Serializable]
	public struct SerializableNullable<T> where T : struct {
		/// <summary>
		/// NOTE:
		/// never change the name of following fields, they are being used
		/// by the custom property drawer to find the serialized values.
		/// </summary>
		[SerializeField] private T _value;
		[SerializeField] private bool _hasValue;

		public readonly bool HasValue => _hasValue;
		public readonly T Value {
			get {
				if (!_hasValue) throw new System.InvalidOperationException("Nullable object must have a value.");
				return _value;
			}
		}

		public SerializableNullable(T value) {
			_value = value;
			_hasValue = true;
		}

		public static implicit operator SerializableNullable<T>(T value) => new(value);

		public readonly T GetValueOrDefault(T defaultValue = default) => _hasValue ? _value : defaultValue;
	}
}