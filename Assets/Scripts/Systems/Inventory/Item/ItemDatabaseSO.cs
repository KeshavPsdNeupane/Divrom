using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Inventory/Item Database")]
public class ItemDatabaseSO : ScriptableObject
{
    [Header("All Items")]
    public List<ItemData> items = new List<ItemData>();

    private Dictionary<int, ItemData> itemDict;
    public void Initialize()
    {
        itemDict = new Dictionary<int, ItemData>();

        foreach (var item in items)
        {
            if (string.IsNullOrEmpty(item.itemId))
            {
                Debug.LogError($"Item '{item.name}' has no ID!");
                continue;
            }

            int hash = HashUtility.GetStableHash(item.itemId);
            item.itemHashId = hash;

            if (itemDict.ContainsKey(hash))
            {
                Debug.LogError($"[Collision] Hash collision between '{itemDict[hash].itemId}' and '{item.itemId}'. Please fix IDs manually.");
            }
            else
            {
                itemDict.Add(hash, item);
            }
        }
    }

    public ItemData GetItemByID(string id)
    {
        if (itemDict == null) Initialize();

        int hash = HashUtility.GetStableHash(id);
        itemDict.TryGetValue(hash, out var item);
        return item;
    }
}
