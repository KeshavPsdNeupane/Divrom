using UnityEditor;
using UnityEngine;
using Kope.Feature.PathFindingNew.Baking;

#if UNITY_EDITOR
namespace Kope.Feature.PathFindingNew.Editor {
	[CustomEditor(typeof(PathFindingGridBaker))]
	public class PathCheckerEditor : UnityEditor.Editor {
		public override void OnInspectorGUI() {
			DrawDefaultInspector();

			EditorGUILayout.Space();
			EditorGUILayout.HelpBox(
				"Grid Data Storage Baking & Validation\n\n" +
				"• Purpose: Scans terrain tilemaps, validates authoring tile components, checks for duplicate placements, and bakes data into high-performance bit-packed Storage Domain assets.\n" +
				"• Visualization: Validation errors (invalid or unassigned tile types, duplicates) are marked in the Scene view using configurable color-coded gizmo spheres.\n\n" +
				"Workflow:\n" +
				"1. Click 'Prepare Pathfinding Data' to scan the terrain tilemap and cache authoring data into editor memory.\n" +
				"2. Click 'Perform Quote-on-Quote Bake' to push memory into the ScriptableObject storage container and trigger serialization.\n" +
				"3. Check console logs for bake duration and tracking statistics.",
				MessageType.Info
			);

			PathFindingGridBaker pathChecker = (PathFindingGridBaker)target;

			EditorGUILayout.Space();
			if (GUILayout.Button("Prepare Pathfinding Data")) {
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