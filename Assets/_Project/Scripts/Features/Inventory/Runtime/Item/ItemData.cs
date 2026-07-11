using UnityEngine;

public enum ItemType {
	None = 0,
	Consumable = 1,
	Armor = 11,
	Weapon = 21,
	Tool = 31,
	Material = 41,
	Quest = 51,
	Miscellaneous = 61
}

[CreateAssetMenu(fileName = "ItemData", menuName = "Scriptable Objects/Item/ItemData")]
public class ItemData : ScriptableObject {
	public string itemId = "Not Set";
	public string itemName = "Not Set Yet";
	public ItemType itemType = ItemType.None;
	public Sprite itemIcon;
	public int maxStackSize = 1;
	public string description = "No Description Set";
}
