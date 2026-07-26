using UnityEngine;

namespace Kope.Core.Attribute {
	/// <summary>
	/// An attribute to mark fields as read-only in the Unity Inspector.
	/// This prevents modification of the field's value through the Inspector.
	/// </summary>
	public class ReadOnlyAttribute : PropertyAttribute {
		// This class is intentionally left empty.
		// The presence of this attribute is used by a custom property drawer
		// to render the field as read-only in the Unity Inspector.
	}
}
