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


	public class ReadOnlyTextAreaAttribute : PropertyAttribute {
		public int minLines;
		public int maxLines;

		public ReadOnlyTextAreaAttribute(int minLines = 3, int maxLines = 5) {
			this.minLines = minLines;
			this.maxLines = maxLines;
		}
	}
	public class MessageAttribute : PropertyAttribute {
		public readonly string text;
		public readonly int minLines;
		public readonly int maxLines;

		public MessageAttribute(string text, int minLines = 1, int maxLines = 5) {
			this.text = text;
			this.minLines = minLines;
			this.maxLines = maxLines;
		}
	}

}
