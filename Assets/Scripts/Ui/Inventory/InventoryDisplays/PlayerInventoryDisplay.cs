using System.Collections.Generic;
using UnityEngine;

public class PlayerInventoryDisplay : InventoryDisplay
{
    [SerializeField] private InventoryHolder inventoryHolder;
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private Transform slotParent;

    protected override void Start()
    {
        base.Start();
        if (inventoryHolder != null)
        {
            primaryInventorySystem = inventoryHolder.PrimaryInventorySystem;
            primaryInventorySystem.onInventoryShotChanged += UpdateSlot;
        }
        else
        {
            Debug.LogWarning($"No inventory assigned to {gameObject}");
            return;
        }
        AssignSlot(primaryInventorySystem);
    }

    public override void AssignSlot(InventorySystem invToDisplay)
    {
        if (slotParent != null)
        {
            foreach (Transform child in slotParent)
                Destroy(child.gameObject);
        }
        slotDictionary = new Dictionary<ItemSlot, ItemSlotUI>();

        for (int i = 0; i < primaryInventorySystem.InventorySize; i++)
        {
            GameObject newSlotObj = Instantiate(slotPrefab, slotParent ?? transform);
            ItemSlotUI slotUI = newSlotObj.GetComponent<ItemSlotUI>();

            slotUI.Init(primaryInventorySystem.InventorySlots[i]);
            slotDictionary.Add(primaryInventorySystem.InventorySlots[i], slotUI);
        }
    }
}
