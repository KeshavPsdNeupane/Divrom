#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace Kope.ModularSpriteAnimation.Editor
{
    [CustomEditor(typeof(StaticAnimationLibraryResolver))]
    public class StaticAnimationLibraryResolverEditor : UnityEditor.Editor
    {
        private static string tempCategory;
        private static string tempLabel;

        [Obsolete("The validation and refresh is now handled directly in StaticAnimationLibraryResolver.OnValidate")]
        private void RefreshAndDrawBase(StaticAnimationLibraryResolver resolver)
        {
            EditorGUI.BeginChangeCheck();
            base.OnInspectorGUI();
            if (EditorGUI.EndChangeCheck())
            {
                Debug.Log("Detected changes in StaticAnimationLibraryResolver, refreshing preview.");
                resolver.RefreshPreview();
            }
        }


        public override void OnInspectorGUI()
        {
            var resolver = (StaticAnimationLibraryResolver)target;
            // 1. Draw the default inspector UI
            base.OnInspectorGUI();

            // Add some spacing
            EditorGUILayout.Space(15);

            // 2. Custom Tool Section
            GUIStyle boxStyle = new(GUI.skin.box)
            {
                padding = new RectOffset(10, 10, 10, 10)
            };
            GUILayout.BeginVertical(boxStyle);

            EditorGUILayout.LabelField("⚡ Quick Animation Snap", EditorStyles.boldLabel);

            tempCategory = EditorGUILayout.TextField("Category", tempCategory);
            tempLabel = EditorGUILayout.TextField("Label", tempLabel);

            EditorGUILayout.Space(5);

            // 3. The Action Button
            GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button("Snap All & Record Keyframes", GUILayout.Height(30)))
            {
                // Find all resolvers managed by the character
                SpriteResolver[] allResolvers = resolver.GetComponentsInChildren<SpriteResolver>();

                if (allResolvers.Length > 0)
                {
                    // RECORD: Tells the Animator (Red Bar) to register a property change
                    Undo.RecordObjects(allResolvers, "Manual Sprite Snap");

                    // APPLY: Call your logic in the main script
                    resolver.SetActiveCategoryAndLabel(tempCategory, tempLabel);

                    // FORCE UPDATE: This is what triggers the Animator to actually drop the keyframe
                    foreach (var r in allResolvers)
                    {
                        // Swaps the actual sprite in the SpriteRenderer immediately
                        r.ResolveSpriteToSpriteRenderer();
                        // Mark as dirty so Unity saves the change to the scene/prefab
                        EditorUtility.SetDirty(r);
                    }

                    Debug.Log($"[Tool] Snapped {allResolvers.Length} parts to {tempCategory}:{tempLabel}");
                }
                else
                {
                    Debug.LogWarning("No SpriteResolvers found in children. Check your character hierarchy.");
                }
            }
            GUI.backgroundColor = Color.white;

            GUILayout.EndVertical();
        }


    }
}
#endif