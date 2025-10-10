using UnityEngine;

public enum ItemType
{
    None = -1,
    Consumable,
    Armor,
    Weapon,
    Tool,
    Material,
    Quest
}

[CreateAssetMenu(fileName = "ItemData", menuName = "Scriptable Objects/Item/ItemData")]
public class ItemData : ScriptableObject
{
    public string itemId = "Not Set";
    public string itemName = "Not Set Yet";
    public ItemType itemType = ItemType.None;
    public Sprite itemIcon;
    public int maxStackSize = 1;
    public string description = "No Description Set";

    [HideInInspector] public int itemHashId;
}
