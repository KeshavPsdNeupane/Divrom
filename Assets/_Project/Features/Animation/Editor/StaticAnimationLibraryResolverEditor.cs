#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(StaticAnimationLibraryResolver))]
public class StaticAnimationLibraryResolverEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUI.BeginChangeCheck();
        base.OnInspectorGUI();
        if (EditorGUI.EndChangeCheck())
        {
            var resolver = (StaticAnimationLibraryResolver)target;
            resolver.RefreshPreview();
        }
    }
}
#endif
