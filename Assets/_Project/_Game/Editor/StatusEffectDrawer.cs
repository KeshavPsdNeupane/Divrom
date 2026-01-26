#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(StatusEffect))]
public class StatusEffectDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Draw foldout
        property.isExpanded = EditorGUI.Foldout(
            new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight),
            property.isExpanded, label);

        if (!property.isExpanded) return;

        EditorGUI.indentLevel++;

        float lineHeight = EditorGUIUtility.singleLineHeight;
        float y = position.y + lineHeight;

        // Draw each field
        DrawProperty(property, "source", ref y, lineHeight);
        DrawProperty(property, "effectName", ref y, lineHeight);
        DrawProperty(property, "statType", ref y, lineHeight);
        DrawProperty(property, "modifierAmount", ref y, lineHeight);
        DrawProperty(property, "totalDuration", ref y, lineHeight);

        // Show “Permanent” label if totalDuration = -1
        var totalDurationProp = property.FindPropertyRelative("totalDuration");
        if (Mathf.Approximately(totalDurationProp.floatValue, StatusEffect.PERMANENT_BUFF_DURATION))
        {
            Rect permanentRect = new(position.x + 15, y, position.width, lineHeight);
            EditorGUI.LabelField(permanentRect, "Permanent");
            y += lineHeight;
        }

        DrawProperty(property, "isPercentage", ref y, lineHeight);
        DrawProperty(property, "isDebuffFromArmor", ref y, lineHeight);
        DrawProperty(property, "isDebuffFromEnemy", ref y, lineHeight);
        DrawProperty(property, "debuffPriority", ref y, lineHeight);
        DrawProperty(property, "description", ref y, lineHeight);

        EditorGUI.indentLevel--;
    }

    private void DrawProperty(SerializedProperty parent, string name, ref float y, float lineHeight)
    {
        var prop = parent.FindPropertyRelative(name);
        if (prop != null)
        {
            Rect rect = new(16, y, EditorGUIUtility.currentViewWidth - 32, lineHeight);
            EditorGUI.PropertyField(rect, prop);
            y += lineHeight + 2;
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight * 15; // approximate, adjust if needed
    }
}
#endif
