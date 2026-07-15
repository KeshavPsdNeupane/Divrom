using UnityEditor;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace Kope.SpriteComposer2D.Editor {
	[CustomEditor(typeof(IEditorLibraryActiveable), true)]
	[CanEditMultipleObjects]
	public class StaticAnimationLibraryResolverEditor : UnityEditor.Editor {
		private string tempCategory;
		private string tempLabel;

		public override void OnInspectorGUI() {
			serializedObject.Update();
			DrawDefaultInspector();

			EditorGUILayout.Space(15);

			// UI Styling
			GUIStyle boxStyle = new(GUI.skin.box) { padding = new RectOffset(10, 10, 10, 10) };
			GUILayout.BeginVertical(boxStyle);

			EditorGUILayout.LabelField("⚡ Quick Animation Snap", EditorStyles.boldLabel);
			tempCategory = EditorGUILayout.TextField("Category", tempCategory);
			tempLabel = EditorGUILayout.TextField("Label", tempLabel);

			EditorGUILayout.Space(5);

			GUI.backgroundColor = Color.cyan;
			if (GUILayout.Button("Snap All & Record Keyframes", GUILayout.Height(30))) {
				ApplySnap();
			}
			GUI.backgroundColor = Color.white;
			GUILayout.EndVertical();

			serializedObject.ApplyModifiedProperties();
		}

		private void ApplySnap() {
			foreach (var t in targets) // Support multi-object editing
			{
				if (t is not IEditorLibraryActiveable resolver)
					continue;

				if (resolver == null) continue;

				SpriteResolver[] allResolvers = resolver.GetComponentsInChildren<SpriteResolver>();
				if (allResolvers != null && allResolvers.Length > 0) {
					Undo.RecordObjects(allResolvers, "Manual Sprite Snap");
					resolver.SetActiveCategoryAndLabel(tempCategory, tempLabel);
					foreach (var r in allResolvers) {
						r.ResolveSpriteToSpriteRenderer();
						EditorUtility.SetDirty(r);
					}
				}
			}
		}
	}
}