#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Kope.Core.Attribute;

[CustomPropertyDrawer(typeof(MessageAttribute))]
public class MessageDrawer : DecoratorDrawer, IDisposable {
	private const float ICON_MIN_HEIGHT = 30f;
	private const float FALLBACK_WIDTH = 300f;

	private float _lastWidth;
	private float _pollLastWidth = -1f;

	public MessageDrawer() {
		// Unity's Inspector doesn't relayout on a pure window resize — it only
		// recomputes cached item heights on things like selection changes or
		// value edits. Polling the width and forcing a repaint is the standard
		// workaround for that caching gap.
		EditorApplication.update += PollForResize;
	}

	// Requires the drawer's IDisposable cleanup to actually be invoked, which
	// depends on your Unity version — verify the box catches up correctly.
	public void Dispose() {
		EditorApplication.update -= PollForResize;
	}

	private void PollForResize() {
		// FIX: We cannot use EditorGUIUtility outside of OnGUI.
		// Instead, we safely check the width of the active EditorWindow.
		var window = EditorWindow.focusedWindow;
		if (window == null) return;

		float currentWidth = window.position.width;

		if (!Mathf.Approximately(currentWidth, _pollLastWidth)) {
			_pollLastWidth = currentWidth;
			InternalEditorUtility.RepaintAllViews();
		}
	}

	public override void OnGUI(Rect position) {
		MessageAttribute attr = (MessageAttribute)attribute;

		// We capture the exact internal layout width here, which is more accurate 
		// than EditorWindow width because it accounts for scrollbars and margins.
		_lastWidth = position.width;

		EditorGUI.HelpBox(position, attr.text, ToUnityMessageType(attr.severity));
	}

	public override float GetHeight() {
		MessageAttribute attr = (MessageAttribute)attribute;

		float lineHeight = EditorGUIUtility.singleLineHeight;
		float minHeight = lineHeight * attr.minLines;
		float maxHeight = lineHeight * attr.maxLines;

		float width = _lastWidth > 0f ? _lastWidth : FALLBACK_WIDTH;

		GUIStyle style = new(EditorStyles.helpBox) { wordWrap = true };
		float contentHeight = style.CalcHeight(new GUIContent(attr.text), width);

		if (attr.severity != MessageSeverity.None)
			contentHeight = Mathf.Max(contentHeight, ICON_MIN_HEIGHT);

		return Mathf.Clamp(contentHeight, minHeight, maxHeight) + 4f;
	}

	private static MessageType ToUnityMessageType(MessageSeverity severity) => severity switch {
		MessageSeverity.Info => MessageType.Info,
		MessageSeverity.Warning => MessageType.Warning,
		MessageSeverity.Error => MessageType.Error,
		_ => MessageType.None,
	};
}
#endif