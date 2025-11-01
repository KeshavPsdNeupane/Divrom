using System;
using UnityEngine;

public class CharacterStatsGUI : MonoBehaviour
{
    [SerializeField] private CharacterStatsSystem characterStats;
    [SerializeField] public bool canShowState = true;
    [SerializeField][Range(0.5f, 3f)] private float scale = 2f;

    private GUIStyle labelStyle;

    private void OnGUI()
    {
        if (!canShowState || characterStats == null)
            return;

        // Initialize style
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label);
        }
        labelStyle.fontSize = Mathf.RoundToInt(12 * scale);
        labelStyle.normal.textColor = Color.white;

        int panelWidth = Mathf.RoundToInt(250 * scale);
        int panelHeight = Mathf.RoundToInt(300 * scale);
        int startX = 10;
        int startY = 10;
        int lineHeight = Mathf.RoundToInt(20 * scale);
        int padding = Mathf.RoundToInt(10 * scale);

        // Panel
        GUI.Box(new Rect(startX, startY, panelWidth, panelHeight), "Character Stats");

        int yOffset = startY + padding;

        // Stats
        GUI.Label(new Rect(startX + padding, yOffset, panelWidth - 2 * padding, lineHeight), "Stats:", labelStyle);
        yOffset += lineHeight;

        foreach (CharacterStatType type in Enum.GetValues(typeof(CharacterStatType)))
        {
            float value = characterStats.GetStatValue(type);
            GUI.Label(new Rect(startX + 2 * padding, yOffset, panelWidth - 3 * padding, lineHeight), $"{type}: {value}", labelStyle);
            yOffset += lineHeight;
        }

        yOffset += padding;
        GUI.Label(new Rect(startX + padding, yOffset, panelWidth - 2 * padding, lineHeight), "Resistances:", labelStyle);
        yOffset += lineHeight;

        foreach (DamageType type in Enum.GetValues(typeof(DamageType)))
        {
            float value = characterStats.GetResistanceValue(type);
            GUI.Label(new Rect(startX + 2 * padding, yOffset, panelWidth - 3 * padding, lineHeight), $"{type}: {value}", labelStyle);
            yOffset += lineHeight;
        }
    }
}
