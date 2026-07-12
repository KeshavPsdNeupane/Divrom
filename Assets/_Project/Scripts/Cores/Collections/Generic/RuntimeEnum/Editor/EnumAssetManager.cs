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

		private Dictionary<int, bool> _foldoutStates = new();
		private Dictionary<int, List<string>> _cachedDependencies = new();

		// UI Toggles
		private bool _showDesignerHint = false;

		private const string DESIGNER_HINT =
			"⚠️ CRITICAL: CHANGING AN ID WILL BREAK EXISTING REFERENCES ⚠️\n\n" +
			"Because your scenes, prefabs, and ScriptableObjects link to these assets using their ID prefix, " +
			"ANY method used to resolve a collision will require you to manually fix broken references afterward.\n\n" +
			"CHOICE A: THE DUPLICATE METHOD (Generates Clean Auto-ID)\n" +
			"1. Select the asset and press Ctrl+D. Delete the old asset.\n" +
			"2. Unity's internal hashing guarantees a brand-new, unique Auto-ID for the copy.\n" +
			"3. Open the drop-down below on the old ID to see exactly which files you now need to re-link.\n\n" +
			"CHOICE B: THE MANUAL METHOD (Forces a Specific ID)\n" +
			"1. Enter a unique number in the 'Change to' field below and click 'Apply New ID'.\n" +
			"2. Use this only if your project relies on strict, predictable ID ranges.\n" +
			"3. Open the drop-down below to see and fix the broken file references.";
		private struct AssetEntry {
			public EnumAsset Asset;
			public int ID;
			public string Name;
			public bool IsManual;
		}

		[MenuItem("Tools/Enum Asset Manager")]
		public static void ShowWindow() {
			var window = GetWindow<EnumAssetManager>("Enum Manager");
			window.minSize = new Vector2(650, 600);
			window.RefreshData();
		}

		private void OnGUI() {
			// --- TOP TOOLBAR ---
			DrawToolbar();

			this._scrollPos = EditorGUILayout.BeginScrollView(this._scrollPos);

			// --- NOTIFICATIONS & HINTS ---
			DrawCollisionsSummary();
			DrawDesignerHintToggle();

			EditorGUILayout.Space(10);

			// --- MAIN LIST ---
			EditorGUILayout.LabelField("Registered Enum Assets", EditorStyles.boldLabel);

			if (this._allAssets.Count == 0) {
				EditorGUILayout.HelpBox("No EnumAssets found in the project. Try scanning again.", MessageType.Info);
			} else {
				foreach (var entry in this._allAssets.ToList()) {
					if (!this.MatchesSearch(entry)) continue;
					DrawAssetCard(entry);
				}
			}

			EditorGUILayout.Space(20);
			EditorGUILayout.EndScrollView();
		}

		private void DrawToolbar() {
			EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

			if (GUILayout.Button(new GUIContent(" Refresh Scan", EditorGUIUtility.IconContent("Refresh").image), EditorStyles.toolbarButton, GUILayout.Width(100))) {
				this.RefreshData();
			}

			GUILayout.Space(10);

			GUILayout.Label("Search:", EditorStyles.miniLabel, GUILayout.Width(45));
			this._searchString = EditorGUILayout.TextField(this._searchString, EditorStyles.toolbarSearchField);
			if (GUILayout.Button("", GUI.skin.FindStyle("ToolbarSearchCancelButton"))) {
				this._searchString = "";
				GUI.FocusControl(null);
			}

			EditorGUILayout.EndHorizontal();
		}

		private void DrawDesignerHintToggle() {
			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
			_showDesignerHint = EditorGUILayout.Foldout(_showDesignerHint, "Show Reference & Collision Guide", true);
			if (_showDesignerHint) {
				EditorGUILayout.HelpBox(DESIGNER_HINT, MessageType.Info);
			}
			EditorGUILayout.EndVertical();
		}

		private void DrawCollisionsSummary() {
			if (this._collisions.Count == 0) return;

			Color defaultColor = GUI.backgroundColor;
			GUI.backgroundColor = new Color(1f, 0.4f, 0.4f, 0.4f);

			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
			GUI.backgroundColor = defaultColor;

			GUILayout.Label(new GUIContent($" {this._collisions.Count} ID COLLISIONS DETECTED!", EditorGUIUtility.IconContent("console.erroricon").image), EditorStyles.boldLabel);
			EditorGUILayout.Space(4);

			foreach (var group in this._collisions) {
				DrawCollisionGroup(group.Key, group.Value);
			}

			EditorGUILayout.EndVertical();
			EditorGUILayout.Space(5);
		}

		private void DrawAssetCard(AssetEntry entry) {
			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
			EditorGUILayout.Space(2);

			// --- ROW 1: Header / Foldout / Asset Display ---
			EditorGUILayout.BeginHorizontal();

			if (!_foldoutStates.ContainsKey(entry.ID)) _foldoutStates[entry.ID] = false;

			// FIXED: Replaced custom layout options on foldout with an explicit narrow label width trick to prevent layout breaking
			EditorGUIUtility.labelWidth = 12;
			_foldoutStates[entry.ID] = EditorGUILayout.Foldout(_foldoutStates[entry.ID], "", true);
			EditorGUIUtility.labelWidth = 0; // Reset to default layout settings

			// Tag/Badge
			if (entry.IsManual) {
				GUI.color = new Color(1f, 0.7f, 0.3f);
				GUILayout.Label(" MANUAL ", EditorStyles.miniButton, GUILayout.Width(60));
			} else {
				GUI.color = new Color(0.4f, 0.8f, 1f);
				GUILayout.Label(" AUTO ", EditorStyles.miniButton, GUILayout.Width(60));
			}
			GUI.color = Color.white;

			// Display Name & Target ID Object Field
			GUILayout.Label(entry.Name, EditorStyles.boldLabel, GUILayout.MinWidth(120));

			GUI.enabled = false;
			EditorGUILayout.ObjectField(GUIContent.none, entry.Asset, typeof(EnumAsset), false, GUILayout.Width(180));
			GUI.enabled = true;

			GUILayout.FlexibleSpace();

			// Row Operations
			if (entry.IsManual) {
				if (GUILayout.Button("Revert to Auto", GUILayout.Width(95))) {
					this.RevertToAuto(entry.Asset);
				}
			}

			if (GUILayout.Button(new GUIContent("Locate", EditorGUIUtility.IconContent("d_Selectable Icon").image), GUILayout.Width(75))) {
				EditorGUIUtility.PingObject(entry.Asset);
				Selection.activeObject = entry.Asset;
			}

			EditorGUILayout.EndHorizontal();

			// --- ROW 2: ID Modification Sub-Area ---
			EditorGUILayout.BeginHorizontal();
			GUILayout.Space(18); // Align neatly with foldout arrow

			GUILayout.Label($"Current ID: {entry.ID}", EditorStyles.miniLabel, GUILayout.Width(100));
			GUILayout.Label("Change to:", EditorStyles.miniLabel, GUILayout.Width(65));

			if (!this._manualEditCache.ContainsKey(entry.Asset)) this._manualEditCache[entry.Asset] = entry.ID;
			this._manualEditCache[entry.Asset] = EditorGUILayout.IntField(this._manualEditCache[entry.Asset], GUILayout.Width(70));

			bool hasChanged = this._manualEditCache[entry.Asset] != entry.ID;
			GUI.enabled = hasChanged;

			if (GUILayout.Button("Apply New ID", EditorStyles.miniButton, GUILayout.Width(90))) {
				string message = $"Are you sure you want to change the ID for '{entry.Name}'?\n\n" +
								 $"FROM: {entry.ID}\n" +
								 $"TO:   {this._manualEditCache[entry.Asset]}\n\n" +
								 "CRITICAL: This changes internal tracking IDs. References in scenes, prefabs, or save files will break and require manual reassignment.";

				if (EditorUtility.DisplayDialog("Confirm ID Migration", message, "Change ID", "Cancel")) {
					this.ApplyManualFix(entry.Asset, this._manualEditCache[entry.Asset]);
				}
			}
			GUI.enabled = true;
			EditorGUILayout.EndHorizontal();

			// --- EXPANDABLE DEPENDENCY SECTION ---
			if (_foldoutStates[entry.ID]) {
				DrawDependencyList(entry);
			}

			EditorGUILayout.Space(2);
			EditorGUILayout.EndVertical();
		}

		private void DrawDependencyList(AssetEntry entry) {
			EditorGUILayout.BeginVertical(EditorStyles.textField);
			GUILayout.Space(4);

			if (!_cachedDependencies.ContainsKey(entry.ID)) {
				_cachedDependencies[entry.ID] = FindAssetsReferencing(entry.Asset);
			}

			var references = _cachedDependencies[entry.ID];

			if (references.Count == 0) {
				GUILayout.Label(new GUIContent(" No direct scene, asset, or prefab references found.", EditorGUIUtility.IconContent("Valid").image), EditorStyles.miniLabel);
			} else {
				GUILayout.Label($" Found in {references.Count} dependencies (Will break if ID shifts):", EditorStyles.miniBoldLabel);

				foreach (var path in references) {
					EditorGUILayout.BeginHorizontal();
					GUILayout.Space(10);

					var obj = AssetDatabase.LoadAssetAtPath<Object>(path);
					if (obj != null) {
						GUIContent content = EditorGUIUtility.ObjectContent(obj, obj.GetType());
						if (GUILayout.Button(content, EditorStyles.label, GUILayout.Height(18))) {
							EditorGUIUtility.PingObject(obj);
							Selection.activeObject = obj;
						}
					} else {
						EditorGUILayout.LabelField(path, EditorStyles.miniLabel);
					}

					EditorGUILayout.EndHorizontal();
				}
			}
			GUILayout.Space(4);
			EditorGUILayout.EndVertical();
		}

		// FIXED: Restored missing structural scanner dependency mapping loop
		private List<string> FindAssetsReferencing(EnumAsset targetAsset) {
			List<string> referencingAssets = new();
			string targetPath = AssetDatabase.GetAssetPath(targetAsset);
			if (string.IsNullOrEmpty(targetPath)) return referencingAssets;

			string[] allGuids = AssetDatabase.FindAssets("t:Prefab t:Scene t:ScriptableObject");

			foreach (var guid in allGuids) {
				string path = AssetDatabase.GUIDToAssetPath(guid);
				string[] dependencies = AssetDatabase.GetDependencies(path, false);

				if (dependencies.Contains(targetPath) && path != targetPath) {
					referencingAssets.Add(path);
				}
			}

			return referencingAssets;
		}

		private void DrawCollisionGroup(int id, List<AssetEntry> assets) {
			EditorGUILayout.BeginVertical(EditorStyles.helpBox);

			EditorGUILayout.BeginHorizontal();
			GUILayout.Label(new GUIContent($" Group conflict on ID: {id}", EditorGUIUtility.IconContent("console.warnicon").image), EditorStyles.boldLabel);
			EditorGUILayout.EndHorizontal();

			foreach (var entry in assets) {
				EditorGUILayout.BeginHorizontal();
				GUILayout.Space(15);

				GUILayout.Label($"• {entry.Name}", EditorStyles.label, GUILayout.MinWidth(150));

				GUILayout.FlexibleSpace();

				if (GUILayout.Button("Auto Fix ID", EditorStyles.miniButton, GUILayout.Width(85))) {
					this.ApplyManualFix(entry.Asset, this.FindAvailableId());
				}
				if (GUILayout.Button("Locate", EditorStyles.miniButton, GUILayout.Width(60))) {
					EditorGUIUtility.PingObject(entry.Asset);
					Selection.activeObject = entry.Asset;
				}
				EditorGUILayout.EndHorizontal();
			}
			EditorGUILayout.EndVertical();
		}

		private void RefreshData() {
			this._allAssets.Clear();
			this._manualEditCache.Clear();
			this._cachedDependencies.Clear();

			string[] guids = AssetDatabase.FindAssets("t:EnumAsset");
			Dictionary<int, List<AssetEntry>> map = new();

			foreach (string guid in guids) {
				var asset = AssetDatabase.LoadAssetAtPath<EnumAsset>(AssetDatabase.GUIDToAssetPath(guid));
				if (!asset) continue;

				SerializedObject so = new(asset);
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

			asset.RevertToAutomaticId();

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

			asset.ManualUpdateId(newId);
			this._manualEditCache[asset] = newId;

			EditorUtility.SetDirty(asset);
			AssetDatabase.SaveAssets();
			this.RefreshData();
		}
	}
}