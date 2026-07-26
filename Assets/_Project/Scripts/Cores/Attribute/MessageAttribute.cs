using UnityEngine;

namespace Kope.Core.Attribute {

	/// <summary>
	/// Which of Unity's built-in HelpBox visuals (icon + color) a MessageAttribute uses.
	/// Mirrors UnityEditor.MessageType's options but is safe to reference from runtime code —
	/// UnityEditor.MessageType itself can't be used outside the editor.
	/// </summary>
	public enum MessageSeverity {
		None,
		Info,
		Warning,
		Error,
	}

	// public class ReadOnlyTextAreaAttribute : PropertyAttribute {
	// 	public const int DEFAULT_MIN_LINES = 3;
	// 	public const int DEFAULT_MAX_LINES = 1000;
	// 	public int minLines;
	// 	public int maxLines;

	// 	public ReadOnlyTextAreaAttribute(int minLines = DEFAULT_MIN_LINES,
	// 	int maxLines = DEFAULT_MAX_LINES) {
	// 		this.minLines = minLines;
	// 		this.maxLines = maxLines;
	// 	}
	// }

	public class MessageAttribute : PropertyAttribute {
		public const int DEFAULT_MIN_LINES = 3;
		public const int DEFAULT_MAX_LINES = 1000;
		public readonly string text;
		public readonly MessageSeverity severity;
		public readonly int minLines;
		public readonly int maxLines;

		public MessageAttribute(string text,
		MessageSeverity severity = MessageSeverity.Info,
		int minLines = DEFAULT_MIN_LINES, int maxLines = DEFAULT_MAX_LINES) {
			this.text = text;
			this.severity = severity;
			this.minLines = minLines;
			this.maxLines = maxLines;
		}
	}
}