using UnityEditor;
using UnityEngine;
#if UNITY_EDITOR
namespace Kope.Feature.PathFindingOld.Editor {
	[CustomEditor(typeof(PathFindingTileBaker))]
	public class PathCheckerEditor : UnityEditor.Editor {
		public override void OnInspectorGUI() {
			DrawDefaultInspector();

			EditorGUILayout.Space();
			EditorGUILayout.HelpBox(
				"Pathfinding Preparation & Validation\n\n" +
				"• Purpose: Validates tilemaps, flags classification errors or duplicates, and builds valid datasets.\n" +
				"• Visualization: Errors and baked region slices are marked in the Scene view with configurable, color-coded indicators matching specific error types and bounding boxes.\n\n" +
				"Workflow:\n" +
				"1. Click 'Prepare Pathfinding Data For Bake' to scan maps.\n" +
				"2. Click 'Perform Quote-on-Quote Bake' to generate slices and visual caches.\n" +
				"3. Inspect color-coded visual markers in the Scene view.",
				MessageType.Info
			);

			PathFindingTileBaker pathChecker = (PathFindingTileBaker)target;

			EditorGUILayout.Space();
			if (GUILayout.Button("Prepare Pathfinding Data For Bake")) {
				pathChecker.PreparePathfindingData();
				EditorUtility.SetDirty(pathChecker);
			}
			if (GUILayout.Button("Perform Quote-on-Quote Bake")) {
				pathChecker.QuoteOnQuoteBake();
				EditorUtility.SetDirty(pathChecker);
			}
		}
	}
}
#endif
