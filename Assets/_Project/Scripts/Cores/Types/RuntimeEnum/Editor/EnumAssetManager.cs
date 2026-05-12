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

		// Dictionary to track temporary ID edits in the UI before applying them
		private Dictionary<EnumAsset, int> _manualEditCache = new();

		private struct AssetEntry {
			public EnumAsset Asset;
			public int ID;
			public string Name;
		}

		[MenuItem("Tools/Enum Asset Manager")]
		public static void ShowWindow() {
			var window = GetWindow<EnumAssetManager>("Enum Manager");
			window.minSize = new Vector2(500, 600);
			window.RefreshData();
		}

		private void OnGUI() {
			// --- Toolbar ---
			EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
			if (GUILayout.Button("Refresh Scan", EditorStyles.toolbarButton)) RefreshData();
			GUILayout.Space(5);
			_searchString = EditorGUILayout.TextField(_searchString, EditorStyles.toolbarSearchField);
			EditorGUILayout.EndHorizontal();

			_scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

			// --- Collisions Section ---
			if (_collisions.Count > 0) {
				EditorGUILayout.HelpBox($"{_collisions.Count} ID COLLISIONS DETECTED!", MessageType.Error);
				foreach (var group in _collisions) {
					DrawCollisionGroup(group.Key, group.Value);
				}
				EditorGUILayout.Space(10);
			}

			// --- Main Asset List ---
			EditorGUILayout.LabelField("Registered Enum Assets", EditorStyles.boldLabel);
			foreach (var entry in _allAssets) {
				if (!MatchesSearch(entry)) continue;

				EditorGUILayout.BeginVertical(EditorStyles.helpBox);

				// Row 1: Info and Navigation
				EditorGUILayout.BeginHorizontal();
				if (GUILayout.Button(entry.ID.ToString(), EditorStyles.linkLabel, GUILayout.Width(70))) EditorGUIUtility.PingObject(entry.Asset);
				if (GUILayout.Button(entry.Name, EditorStyles.label)) EditorGUIUtility.PingObject(entry.Asset);
				if (GUILayout.Button("Open", GUILayout.Width(50))) Selection.activeObject = entry.Asset;
				EditorGUILayout.EndHorizontal();

				// Row 2: Manual Edit Field (For testing/forcing IDs)
				EditorGUILayout.BeginHorizontal();
				GUILayout.Label("Manual Change:", EditorStyles.miniLabel, GUILayout.Width(90));

				if (!_manualEditCache.ContainsKey(entry.Asset)) _manualEditCache[entry.Asset] = entry.ID;
				_manualEditCache[entry.Asset] = EditorGUILayout.IntField(_manualEditCache[entry.Asset], GUILayout.Width(80));

				GUI.enabled = _manualEditCache[entry.Asset] != entry.ID;

				// --- Tooltip added here ---
				GUIContent updateBtnContent = new GUIContent("Update Prefix",
					"CAUTION: If you update this ID, you must manually fix all references in Scenes, Prefabs, and Assets that point to this Enum. " +
					"Existing references will break.");

				if (GUILayout.Button(updateBtnContent, EditorStyles.miniButton, GUILayout.Width(100))) {
					if (EditorUtility.DisplayDialog("Confirm Manual ID Change",
						"Changing this ID will break existing references in your project. You will need to re-assign them manually. Proceed?", "Yes", "Cancel")) {
						ApplyManualFix(entry.Asset, _manualEditCache[entry.Asset]);
					}
				}
				GUI.enabled = true;

				GUILayout.FlexibleSpace();
				EditorGUILayout.EndHorizontal();

				EditorGUILayout.EndVertical();
			}

			// --- Footer Refresh ---
			EditorGUILayout.Space(10);
			if (GUILayout.Button("Full Refresh Project Scan", GUILayout.Height(30))) {
				RefreshData();
			}

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

				GUIContent suggestBtnContent = new GUIContent("Suggest Fix", "Finds the next available ID and applies it. Note: References will still need manual fixing.");

				if (GUILayout.Button(suggestBtnContent, GUILayout.Width(100))) {
					ApplyManualFix(entry.Asset, FindAvailableId());
				}
				if (GUILayout.Button("Locate", GUILayout.Width(60))) EditorGUIUtility.PingObject(entry.Asset);
				EditorGUILayout.EndHorizontal();
			}
			EditorGUILayout.EndVertical();
		}

		private void RefreshData() {
			_allAssets.Clear();
			_manualEditCache.Clear();
			string[] guids = AssetDatabase.FindAssets("t:EnumAsset");
			Dictionary<int, List<AssetEntry>> map = new();

			foreach (string guid in guids) {
				var asset = AssetDatabase.LoadAssetAtPath<EnumAsset>(AssetDatabase.GUIDToAssetPath(guid));
				if (!asset) continue;

				var so = new SerializedObject(asset);
				int id = so.FindProperty("_enumAssetId").intValue;
				var entry = new AssetEntry { Asset = asset, ID = id, Name = asset.name };

				_allAssets.Add(entry);
				if (!map.ContainsKey(id)) map[id] = new List<AssetEntry>();
				map[id].Add(entry);

				// Initialize the edit cache with current value
				_manualEditCache[asset] = id;
			}

			_collisions = map.Where(kvp => kvp.Value.Count > 1).ToDictionary(k => k.Key, v => v.Value);
			_allAssets = _allAssets.OrderBy(a => a.ID).ToList();
		}

		private int FindAvailableId() {
			var used = _allAssets.Select(a => a.ID).ToHashSet();
			for (int i = EnumAsset.MIN_ASSET_PREFIX; i <= EnumAsset.MAX_ASSET_PREFIX; i++) {
				if (!used.Contains(i)) return i;
			}
			return -1;
		}

		private bool MatchesSearch(AssetEntry entry) {
			if (string.IsNullOrEmpty(_searchString)) return true;
			return entry.Name.ToLower().Contains(_searchString.ToLower()) || entry.ID.ToString().Contains(_searchString);
		}

		private void ApplyManualFix(EnumAsset asset, int newId) {
			if (newId < EnumAsset.MIN_ASSET_PREFIX || newId > EnumAsset.MAX_ASSET_PREFIX) {
				Debug.LogError($"[EnumManager] ID {newId} is out of range ({EnumAsset.MIN_ASSET_PREFIX}-{EnumAsset.MAX_ASSET_PREFIX})");
				return;
			}

			Undo.RecordObject(asset, "Manual ID Change");
			asset.ManualUpdateId(newId);
			EditorUtility.SetDirty(asset);
			AssetDatabase.SaveAssets();
			RefreshData();
			Debug.Log($"[EnumManager] {asset.name} updated to ID {newId}. Internal instances recalculated. Please re-assign project references manually.");
		}
	}
}