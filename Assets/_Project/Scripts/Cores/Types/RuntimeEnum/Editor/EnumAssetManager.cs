using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace Kope.Core.Type.EnumAsset.EditorTools {
	public class EnumAssetManager : EditorWindow {
		private Vector2 _scrollPos;
		private string _searchString = "";
		private List<AssetEntry> _allAssets = new();
		private Dictionary<int, List<AssetEntry>> _collisions = new();
		private Dictionary<EnumAsset, int> _manualEditCache = new();

		private const string DESIGNER_HINT =
			"RECOMMENDED COLLISION RESOLUTION:\n" +
			"1. THE DUPLICATE METHOD (Preferred): Select the asset and press Ctrl+D. Delete the original and use the new copy. " +
			"Unity's internal hashing ensures a unique ID for the new asset.\n" +
			"2. THE MANUAL METHOD: Directly edit the ID below or use 'Revert to Auto'. " +
			"Manual edits lack the safety of Unity's GUID generation and require manual re-linking of references.";

		private struct AssetEntry {
			public EnumAsset Asset;
			public int ID;
			public string Name;
			public bool IsManual;
		}

		[MenuItem("Tools/Enum Asset Manager")]
		public static void ShowWindow() {
			var window = GetWindow<EnumAssetManager>("Enum Manager");
			window.minSize = new Vector2(550, 600);
			window.RefreshData();
		}

		private void OnGUI() {
			EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
			if (GUILayout.Button("Refresh Scan", EditorStyles.toolbarButton)) this.RefreshData();
			GUILayout.Space(5);
			this._searchString = EditorGUILayout.TextField(this._searchString, EditorStyles.toolbarSearchField);
			EditorGUILayout.EndHorizontal();

			this._scrollPos = EditorGUILayout.BeginScrollView(this._scrollPos);

			EditorGUILayout.HelpBox(DESIGNER_HINT, MessageType.Info);
			EditorGUILayout.Space(5);

			if (this._collisions.Count > 0) {
				EditorGUILayout.HelpBox($"{this._collisions.Count} ID COLLISIONS DETECTED!", MessageType.Error);
				foreach (var group in this._collisions) {
					this.DrawCollisionGroup(group.Key, group.Value);
				}
				EditorGUILayout.Space(10);
			}

			EditorGUILayout.LabelField("Registered Enum Assets", EditorStyles.boldLabel);
			foreach (var entry in this._allAssets.ToList()) {
				if (!this.MatchesSearch(entry)) continue;

				EditorGUILayout.BeginVertical(EditorStyles.helpBox);

				EditorGUILayout.BeginHorizontal();
				string prefix = entry.IsManual ? "[M] " : "[A] ";
				if (GUILayout.Button(prefix + entry.ID, EditorStyles.linkLabel, GUILayout.Width(80))) EditorGUIUtility.PingObject(entry.Asset);
				EditorGUILayout.LabelField(entry.Name, EditorStyles.boldLabel);

				if (entry.IsManual) {
					if (GUILayout.Button("Revert to Auto", GUILayout.Width(100))) {
						this.RevertToAuto(entry.Asset);
					}
				}

				if (GUILayout.Button("Open", GUILayout.Width(50))) Selection.activeObject = entry.Asset;
				EditorGUILayout.EndHorizontal();

				EditorGUILayout.BeginHorizontal();
				GUILayout.Label("Manual Change:", EditorStyles.miniLabel, GUILayout.Width(90));

				if (!this._manualEditCache.ContainsKey(entry.Asset)) this._manualEditCache[entry.Asset] = entry.ID;
				this._manualEditCache[entry.Asset] = EditorGUILayout.IntField(this._manualEditCache[entry.Asset], GUILayout.Width(80));

				GUI.enabled = this._manualEditCache[entry.Asset] != entry.ID;
				if (GUILayout.Button("Update Prefix", EditorStyles.miniButton, GUILayout.Width(100))) {
					string message = $"Are you sure you want to change the ID for '{entry.Name}'?\n\n" +
									 $"FROM: {entry.ID}\n" +
									 $"TO:   {this._manualEditCache[entry.Asset]}\n\n" +
									 "CRITICAL: This will change the Internal IDs of ALL entries in this asset. " +
									 "Any existing references in scenes, prefabs, or save files will likely BREAK and require manual fixing.";

					if (EditorUtility.DisplayDialog("Confirm ID Change", message, "Update ID", "Cancel")) {
						this.ApplyManualFix(entry.Asset, this._manualEditCache[entry.Asset]);
					}
				}
				GUI.enabled = true;
				EditorGUILayout.EndHorizontal();

				EditorGUILayout.EndVertical();
			}

			EditorGUILayout.Space(10);
			if (GUILayout.Button("Full Refresh Project Scan", GUILayout.Height(30))) this.RefreshData();

			EditorGUILayout.EndScrollView();
		}

		private void DrawCollisionGroup(int id, List<AssetEntry> assets) {
			EditorGUILayout.BeginVertical("box");
			GUI.color = new Color(1f, 0.8f, 0.8f);
			EditorGUILayout.LabelField($"Colliding Prefix: {id}", EditorStyles.boldLabel);
			GUI.color = Color.white;

			foreach (var entry in assets) {
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.LabelField($"-> {entry.Name}");
				if (GUILayout.Button("Suggest Fix", GUILayout.Width(100))) this.ApplyManualFix(entry.Asset, this.FindAvailableId());
				if (GUILayout.Button("Locate", GUILayout.Width(60))) EditorGUIUtility.PingObject(entry.Asset);
				EditorGUILayout.EndHorizontal();
			}
			EditorGUILayout.EndVertical();
		}

		private void RefreshData() {
			this._allAssets.Clear();
			this._manualEditCache.Clear();
			string[] guids = AssetDatabase.FindAssets("t:EnumAsset");
			Dictionary<int, List<AssetEntry>> map = new();

			foreach (string guid in guids) {
				var asset = AssetDatabase.LoadAssetAtPath<EnumAsset>(AssetDatabase.GUIDToAssetPath(guid));
				if (!asset) continue;

				SerializedObject so = new SerializedObject(asset);
				int id = so.FindProperty("_enumAssetId").intValue;
				bool isManual = so.FindProperty("_isManualId").boolValue;

				var entry = new AssetEntry { Asset = asset, ID = id, Name = asset.name, IsManual = isManual };

				this._allAssets.Add(entry);
				if (!map.ContainsKey(id)) map[id] = new List<AssetEntry>();
				map[id].Add(entry);

				this._manualEditCache[asset] = id;
			}

			this._collisions = map.Where(kvp => kvp.Value.Count > 1).ToDictionary(k => k.Key, v => v.Value);
			this._allAssets = this._allAssets.OrderBy(a => a.ID).ToList();
		}

		private void RevertToAuto(EnumAsset asset) {
			if (asset == null) return;

			// Revert the asset logic (No Undo)
			asset.RevertToAutomaticId();

			// Update the text box cache to match the new Auto ID
			SerializedObject so = new(asset);
			this._manualEditCache[asset] = so.FindProperty("_enumAssetId").intValue;

			EditorUtility.SetDirty(asset);
			AssetDatabase.SaveAssets();
			this.RefreshData();
		}

		private int FindAvailableId() {
			var used = this._allAssets.Select(a => a.ID).ToHashSet();
			for (int i = EnumAsset.MIN_ASSET_PREFIX; i <= EnumAsset.MAX_ASSET_PREFIX; i++) {
				if (!used.Contains(i)) return i;
			}
			return -1;
		}

		private bool MatchesSearch(AssetEntry entry) {
			if (string.IsNullOrEmpty(this._searchString)) return true;
			return entry.Name.ToLower().Contains(this._searchString.ToLower()) || entry.ID.ToString().Contains(this._searchString);
		}

		private void ApplyManualFix(EnumAsset asset, int newId) {
			if (newId < EnumAsset.MIN_ASSET_PREFIX || newId > EnumAsset.MAX_ASSET_PREFIX) return;

			// Update asset logic (No Undo)
			asset.ManualUpdateId(newId);
			this._manualEditCache[asset] = newId;

			EditorUtility.SetDirty(asset);
			AssetDatabase.SaveAssets();
			this.RefreshData();
		}
	}
}