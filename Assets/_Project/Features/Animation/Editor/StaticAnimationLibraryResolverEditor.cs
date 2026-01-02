#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StaticAnimationLibraryResolver))]
public class StaticAnimationLibraryResolverEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var resolver = (StaticAnimationLibraryResolver)target;

        // Draw the default inspector fields (Race, Gender, Category/Label strings, etc.)
        EditorGUI.BeginChangeCheck();
        base.OnInspectorGUI();

        if (EditorGUI.EndChangeCheck())
        {
            // If any value (like Race or Gender) changed, refresh the assets
            resolver.RefreshPreview();
        }

        // Add some spacing before the button
        EditorGUILayout.Space(10);

        // Optional: Change button color to make it look like a "tool" button
        GUI.backgroundColor = new Color(0.7f, 1f, 0.7f); // Light Green

        if (GUILayout.Button("Force Set All Labels", GUILayout.Height(30)))
        {
            // This calls your new Debugging API function
            resolver.SetActiveCategoryAndLabel();
        }

        // Reset color so other editors aren't affected
        GUI.backgroundColor = Color.white;
    }
}
#endif