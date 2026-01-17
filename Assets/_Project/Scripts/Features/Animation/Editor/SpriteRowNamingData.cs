using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct SpriteRowData
{
    public string category;           // e.g., "Walk", "Run", "Death"
    public List<string> subCategory;  // e.g., "Up", "Left", "Down", "Right"; empty/null for single-row animations
}

[System.Serializable]
public struct SpriteRowDataSpecial
{
    public SpriteRowData rowData;      // normal row
    public SpriteRowData specialData;  // optional special frames (like Idle)
    public bool hasSpecialSprites;
    public int specialStartIndex;      // at which frame index to insert special frames
    public int specialSize;            // how many frames to treat as special
}

[CreateAssetMenu(fileName = "SpriteRowNamingData", menuName = "Sprite Tools/Row Naming Data")]
public class SpriteRowNamingData : ScriptableObject
{
    public List<SpriteRowDataSpecial> rows = new();
}
