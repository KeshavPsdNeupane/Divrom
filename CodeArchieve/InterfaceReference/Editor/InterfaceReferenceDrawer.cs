#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using ZLinq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kope.Core.Collections.Serialization.Editor {
	[CustomPropertyDrawer(typeof(InterfaceReference<>))]
	public class InterfaceReferenceDrawer : PropertyDrawer {
		private static readonly HashSet<string> _loggedProperties = new();

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
			SerializedProperty objectProp = property.FindPropertyRelative("underlyingObject");

			System.Type interfaceType = GetInterfaceType(fieldInfo.FieldType);
			if (interfaceType == null) {
				EditorGUI.HelpBox(position, $"Architecture Error: Field '{property.name}' has an invalid generic interface argument.", MessageType.Error);
				return;
			}

			// ── AGGRESSIVE CONSOLE-SAFE DIAGNOSTIC LAYER ─────────────────────────────
			UnityEngine.Object assignedObject = objectProp.objectReferenceValue;
			if (assignedObject != null) {
				bool isStaleOrBroken = !IsInterfaceCompatible(assignedObject, interfaceType);

				if (isStaleOrBroken) {
					// Generate a completely unique key for this exact field instance
					string uniquePropertyKey = $"{property.serializedObject.targetObject.GetInstanceID()}:{property.propertyPath}";

					// Log to the console EXACTLY once per broken instance
					if (!_loggedProperties.Contains(uniquePropertyKey)) {
						_loggedProperties.Add(uniquePropertyKey);

						string consoleError = assignedObject is GameObject
							? $"[Kope Architecture] CRITICAL: GameObject '{assignedObject.name}' on field '{property.name}' has lost or is missing a component implementing '{interfaceType.Name}'!"
							: $"[Kope Architecture] CRITICAL: Asset '{assignedObject.name}' assigned to field '{property.name}' does not implement interface '{interfaceType.Name}'!";

						Debug.LogError($"<color=#ff4d4d><b>{consoleError}</b></color>", property.serializedObject.targetObject);
					}

					// Render the visual warning box in the inspector
					Color oldColor = GUI.color;
					GUI.color = new Color(1f, 0.3f, 0.3f, 1f);

					Rect errorRect = new(position.x, position.y, position.width, position.height);
					string errorMessage = assignedObject is GameObject
						? $"[MISSING COMPONENT] '{assignedObject.name}' is missing '{interfaceType.Name}'!"
						: $"[TYPE MISMATCH] Asset '{assignedObject.name}' does not implement '{interfaceType.Name}'!";

					EditorGUI.HelpBox(errorRect, errorMessage, MessageType.Error);

					Rect clearButtonRect = new(position.x + position.width - 70, position.y + 2, 68, position.height - 4);
					GUI.color = Color.white;
					if (GUI.Button(clearButtonRect, "Fix / Clear", EditorStyles.miniButton)) {
						// Clear tracking registration so it can warn again if re-assigned incorrectly later
						_loggedProperties.Remove(uniquePropertyKey);
						ValidateAndAssign(null, objectProp, interfaceType);
					}

					GUI.color = oldColor;
					return;
				}
			}
			// ──────────────────────────────────────────────────────────────────────────

			position = EditorGUI.PrefixLabel(position, label);

			Rect fieldRect = new(position.x, position.y, position.width - 22, position.height);
			Rect buttonRect = new(position.x + position.width - 20, position.y, 20, position.height);

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
					EditorGUIUtility.PingObject(current);
					evt.Use();
					break;

				case EventType.DragUpdated:
				case EventType.DragPerform:
					UnityEngine.Object dragged = DragAndDrop.objectReferences.AsValueEnumerable()
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

		private static bool IsInterfaceCompatible(UnityEngine.Object obj, System.Type interfaceType) {
			if (obj == null) return false;

			return obj is GameObject go
				? go.GetComponent(interfaceType) != null
				: interfaceType.IsInstanceOfType(obj);
		}

		private void ValidateAndAssign(UnityEngine.Object obj, SerializedProperty prop, System.Type interfaceType) {
			prop.serializedObject.Update();

			if (obj == null) {
				prop.objectReferenceValue = null;
			} else if (IsInterfaceCompatible(obj, interfaceType)) {
				prop.objectReferenceValue = obj;
			} else {
				Debug.LogError($"<color=#ff4d4d><b>[Kope Architecture] Validation Failure:</b></color> Assigned item '{obj.name}' rejected. It does not implement contract interface '{interfaceType.Name}'.");
				prop.objectReferenceValue = null;
			}

			prop.serializedObject.ApplyModifiedProperties();
		}

		private static System.Type GetInterfaceType(System.Type type) {
			while (type != null) {
				if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(InterfaceReference<>)) {
					return type.GetGenericArguments()[0];
				}
				type = type.BaseType;
			}
			return null;
		}
	}

	public class InterfacePickerDropdown : AdvancedDropdown {
		private readonly System.Type _interfaceType;
		private readonly Action<UnityEngine.Object> _onSelected;

		public InterfacePickerDropdown(AdvancedDropdownState state, System.Type interfaceType, Action<UnityEngine.Object> onSelected) : base(state) {
			_interfaceType = interfaceType;
			_onSelected = onSelected;
			this.minimumSize = new Vector2(280, 400);
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

			if (sceneGroup.children.AsValueEnumerable().Any()) root.AddChild(sceneGroup);

			// ── OPTIMIZED PROJECT ASSETS SEARCH ──────────────────────────────────────
			AdvancedDropdownItem assetGroup = new("Project Assets");

			string[] soGuids = AssetDatabase.FindAssets("t:ScriptableObject");
			foreach (string guid in soGuids) {
				string path = AssetDatabase.GUIDToAssetPath(guid);
				System.Type assetType = AssetDatabase.GetMainAssetTypeAtPath(path);

				if (assetType != null && _interfaceType.IsAssignableFrom(assetType)) {
					UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
					if (asset != null) assetGroup.AddChild(new ObjectDropdownItem(asset, $"[SO] {asset.name}"));
				}
			}

			string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
			foreach (string guid in prefabGuids) {
				string path = AssetDatabase.GUIDToAssetPath(guid);
				GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
				if (prefab != null && prefab.GetComponent(_interfaceType) != null) {
					assetGroup.AddChild(new ObjectDropdownItem(prefab, $"[Prefab] {prefab.name}"));
				}
			}
			if (assetGroup.children.AsValueEnumerable().Any()) root.AddChild(assetGroup);
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