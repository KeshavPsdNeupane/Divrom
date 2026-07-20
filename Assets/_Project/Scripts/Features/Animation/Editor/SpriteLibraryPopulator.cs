using UnityEngine;
using UnityEditor;
using UnityEngine.U2D.Animation;
using System.IO;
using ZLinq;

namespace Kope.ModularSpriteAnimation.Editor {

	public class SpriteLibraryPopulator : EditorWindow {
		private Texture2D spriteSheet;
		private SpriteLibraryAsset dummyLibrary;

		[MenuItem("Tools/Populate Library From Dummy")]
		public static void ShowWindow() {
			GetWindow<SpriteLibraryPopulator>("Library Populator");
		}

		private void OnGUI() {
			GUILayout.Label("Source Data", EditorStyles.boldLabel);
			spriteSheet = (Texture2D)EditorGUILayout.ObjectField("New Sprite Sheet", spriteSheet, typeof(Texture2D), false);
			dummyLibrary = (SpriteLibraryAsset)EditorGUILayout.ObjectField("Dummy Template", dummyLibrary, typeof(SpriteLibraryAsset), false);

			EditorGUILayout.Space();

			if (GUILayout.Button("Create New Library Instance", GUILayout.Height(30))) {
				if (spriteSheet != null && dummyLibrary != null)
					ProcessLibrary();
				else
					EditorUtility.DisplayDialog("Error", "Please assign both the Sprite Sheet and the Dummy Library.", "OK");
			}
		}

		private void ProcessLibrary() {
			string path = AssetDatabase.GetAssetPath(spriteSheet);
			string directory = Path.GetDirectoryName(path);

			// 1. Get all sprites from the new sheet
			var sheetSprites = AssetDatabase.LoadAllAssetsAtPath(path).AsValueEnumerable()
				.OfType<Sprite>()
				.ToDictionary(s => s.name, s => s);

			// 2. Create the new asset instance
			SpriteLibraryAsset newLibrary = CreateInstance<SpriteLibraryAsset>();

			// SET THE DUMMY AS THE MAIN LIBRARY (Parent)
			// This makes the new library inherit the structure and point back to the dumm


			// 3. Loop through Categories in the Dummy
			foreach (string category in dummyLibrary.GetCategoryNames()) {
				// 4. Loop through Labels in each Category
				foreach (string label in dummyLibrary.GetCategoryLabelNames(category)) {

					if (sheetSprites.TryGetValue(label, out Sprite foundSpriteOnlyLabel)) {
						newLibrary.AddCategoryLabel(foundSpriteOnlyLabel, category, label);
					} else {
						newLibrary.AddCategoryLabel(
							dummyLibrary.GetSprite(category, label),
							category,
							label
						);
					}
				}
			}

			// 5. Save the asset to disk (Making it a permanent asset)
			string savePath = $"{directory}/{spriteSheet.name}.asset";

			// Ensure the filename is unique to prevent overwriting accidentally
			savePath = AssetDatabase.GenerateUniqueAssetPath(savePath);

			AssetDatabase.CreateAsset(newLibrary, savePath);

			// Important: Notify Unity that the object has changed and needs saving
			EditorUtility.SetDirty(newLibrary);

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			Debug.Log($"Successfully created and saved permanent asset: {savePath}");
			Selection.activeObject = newLibrary;
		}
	}
}