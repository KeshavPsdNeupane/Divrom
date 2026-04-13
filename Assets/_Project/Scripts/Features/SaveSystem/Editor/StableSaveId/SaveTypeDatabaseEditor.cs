using UnityEditor;
using UnityEngine;

namespace Kope.SaveSystem {
	[CustomEditor(typeof(SaveTypeDatabase))]
	public sealed class SaveTypeDatabaseEditor : Editor {
		public override void OnInspectorGUI() {
			DrawDefaultInspector();

			EditorGUILayout.Space();
			if (GUILayout.Button("Register Project Types")) {
				var database = (SaveTypeDatabase)target;
				database.RebuildFromProject();
				EditorUtility.SetDirty(database);
				AssetDatabase.SaveAssets();
				AssetDatabase.Refresh();
			}
		}
	}
}