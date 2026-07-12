#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kope.Core.Collections.Serialization.Editor {
	[CustomPropertyDrawer(typeof(InterfaceReference<>))]
	public class InterfaceReferenceDrawer : PropertyDrawer {

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
			SerializedProperty objectProp = property.FindPropertyRelative("underlyingObject");

			System.Type interfaceType = fieldInfo.FieldType.IsGenericType
				? fieldInfo.FieldType.GetGenericArguments()[0]
				: fieldInfo.FieldType.BaseType.GetGenericArguments()[0];

			position = EditorGUI.PrefixLabel(position, label);

			Rect fieldRect = new(position.x, position.y, position.width - 20, position.height);
			Rect buttonRect = new(position.x + position.width - 18, position.y, 18, position.height);

			DrawDragDropField(fieldRect, objectProp, interfaceType);

			if (GUI.Button(buttonRect, "○", EditorStyles.miniButton)) {
				InterfacePickerDropdown dropdown = new(new AdvancedDropdownState(), interfaceType, selectedObj => {
					objectProp.serializedObject.Update();
					objectProp.objectReferenceValue = selectedObj;
					objectProp.serializedObject.ApplyModifiedProperties();
				});

				dropdown.Show(position);
			}
		}

		// Manual replacement for EditorGUI.ObjectField — same drag-and-drop
		// acceptance, but no built-in Object Picker button attached.
		private void DrawDragDropField(Rect rect, SerializedProperty objectProp, System.Type interfaceType) {
			UnityEngine.Object current = objectProp.objectReferenceValue;

			GUIContent content = current != null
				? EditorGUIUtility.ObjectContent(current, current.GetType())
				: new GUIContent($"None ({interfaceType.Name})");

			GUI.Box(rect, content, EditorStyles.objectField);

			Event evt = Event.current;
			if (!rect.Contains(evt.mousePosition)) return;

			switch (evt.type) {
				case EventType.MouseDown when evt.button == 0 && current != null:
					// Click-to-ping, same convenience a normal ObjectField gives you,
					// without opening the picker.
					EditorGUIUtility.PingObject(current);
					evt.Use();
					break;

				case EventType.DragUpdated:
				case EventType.DragPerform:
					UnityEngine.Object dragged = DragAndDrop.objectReferences
						.FirstOrDefault(obj => IsInterfaceCompatible(obj, interfaceType));

					DragAndDrop.visualMode = dragged != null
						? DragAndDropVisualMode.Link
						: DragAndDropVisualMode.Rejected;

					if (evt.type == EventType.DragPerform && dragged != null) {
						DragAndDrop.AcceptDrag();
						ValidateAndAssign(dragged, objectProp, interfaceType);
						evt.Use();
					}
					break;
			}
		}

		// Silent check used while hovering a drag — no console spam per frame.
		private static bool IsInterfaceCompatible(UnityEngine.Object obj, System.Type interfaceType) {
			return obj is GameObject go
				? go.GetComponent(interfaceType) != null
				: interfaceType.IsInstanceOfType(obj);
		}

		private void ValidateAndAssign(UnityEngine.Object obj, SerializedProperty prop, System.Type interfaceType) {
			if (obj == null) {
				prop.objectReferenceValue = null;
				return;
			}

			bool isValid = obj is GameObject go ? go.GetComponent(interfaceType) != null : interfaceType.IsInstanceOfType(obj);
			if (isValid) {
				prop.objectReferenceValue = obj;
			} else {
				Debug.LogWarning($"[Kope Architecture] Rejected assignment: '{obj.name}' does not implement '{interfaceType.Name}'.");
				prop.objectReferenceValue = null;
			}
		}
	}

	public class InterfacePickerDropdown : AdvancedDropdown {
		private readonly System.Type _interfaceType;
		private readonly Action<UnityEngine.Object> _onSelected;

		public InterfacePickerDropdown(AdvancedDropdownState state, System.Type interfaceType, Action<UnityEngine.Object> onSelected) : base(state) {
			_interfaceType = interfaceType;
			_onSelected = onSelected;
			this.minimumSize = new Vector2(250, 350);
		}

		protected override AdvancedDropdownItem BuildRoot() {
			AdvancedDropdownItem root = new($"Select {_interfaceType.Name}");
			root.AddChild(new ObjectDropdownItem(null, "None (Empty)"));

			// ── SCENE & PREFAB STAGE SEARCH ──────────────────────────────────────────
			AdvancedDropdownItem sceneGroup = new("Current Scene / Prefab Stage");

			List<GameObject> rootObjects = new();
			var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();

			if (prefabStage != null) {
				if (prefabStage.prefabContentsRoot != null) {
					rootObjects.Add(prefabStage.prefabContentsRoot);
				}
			} else {
				Scene activeScene = SceneManager.GetActiveScene();
				if (activeScene.IsValid()) {
					rootObjects.AddRange(activeScene.GetRootGameObjects());
				}
			}

			foreach (var rootGo in rootObjects) {
				var components = rootGo.GetComponentsInChildren<MonoBehaviour>(true);
				foreach (var comp in components) {
					if (comp != null && _interfaceType.IsInstanceOfType(comp)) {
						sceneGroup.AddChild(new ObjectDropdownItem(comp.gameObject, $"[GO] {comp.gameObject.name} ({comp.GetType().Name})"));
					}
				}
			}

			if (sceneGroup.children.Any()) root.AddChild(sceneGroup);

			// ── OPTIMIZED PROJECT ASSETS SEARCH ──────────────────────────────────────
			AdvancedDropdownItem assetGroup = new("Project Assets");

			// 1. Scan ScriptableObject assets by main layout type metadata first to bypass deep deserialization log spam
			string[] soGuids = AssetDatabase.FindAssets("t:ScriptableObject");
			foreach (string guid in soGuids) {
				string path = AssetDatabase.GUIDToAssetPath(guid);
				System.Type assetType = AssetDatabase.GetMainAssetTypeAtPath(path);

				if (assetType != null && _interfaceType.IsAssignableFrom(assetType)) {
					UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
					if (asset != null) assetGroup.AddChild(new ObjectDropdownItem(asset, asset.name));
				}
			}

			// 2. Scan Prefab asset components cleanly without opening unrelated data assets
			string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
			foreach (string guid in prefabGuids) {
				string path = AssetDatabase.GUIDToAssetPath(guid);
				GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
				if (prefab != null && prefab.GetComponent(_interfaceType) != null) {
					assetGroup.AddChild(new ObjectDropdownItem(prefab, prefab.name));
				}
			}

			if (assetGroup.children.Any()) root.AddChild(assetGroup);

			return root;
		}

		protected override void ItemSelected(AdvancedDropdownItem item) {
			if (item is ObjectDropdownItem objectItem) {
				_onSelected?.Invoke(objectItem.Target);
			}
		}
	}

	public class ObjectDropdownItem : AdvancedDropdownItem {
		public UnityEngine.Object Target { get; }
		public ObjectDropdownItem(UnityEngine.Object target, string displayName) : base(displayName) {
			Target = target;
		}
	}
}
#endif