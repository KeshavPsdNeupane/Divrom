using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(ItemDatabaseSO))]
public class ItemDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ItemDatabaseSO database = (ItemDatabaseSO) target;

        if (GUILayout.Button("Check for Hash Collisions"))
        {
            CheckCollisions(database);
        }
    }

    private void CheckCollisions(ItemDatabaseSO database)
    {
        Dictionary<int, List<ItemData>> hashDict = new Dictionary<int, List<ItemData>>();
        bool collisionFound = false;

        foreach (var item in database.items)
        {
            if (string.IsNullOrEmpty(item.itemId))
            {
                Debug.LogWarning($"Item '{item.name}' has no ID!");
                continue;
            }

            int hash = HashUtility.GetStableHash(item.itemId);

            if (!hashDict.ContainsKey(hash))
                hashDict[hash] = new List<ItemData>();

            hashDict[hash].Add(item);
        }

        foreach (var pair in hashDict)
        {
            if (pair.Value.Count > 1)
            {
                collisionFound = true;
                string ids = string.Join(", ", pair.Value.ConvertAll(i => i.itemId).ToArray());
                Debug.LogError($"Hash collision detected for hash {pair.Key}: {ids}");
            }
        }

        if (!collisionFound)
            Debug.Log("No hash collisions detected!");
    }
}
