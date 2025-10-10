using System.Collections.Generic;
using UnityEngine;

public class PlayerInventoryDisplay : InventoryDisplay
{
    [SerializeField] private InventoryHolder inventoryHolder;
    [SerializeField] private ItemSlotUI[] slotsUI;
    protected override void Start()
    {
        base.Start();
        if (this.inventoryHolder != null)
        {
            this.primaryInventorySystem = this.inventoryHolder.PrimaryInventorySystem;
            this.primaryInventorySystem.onInventoryShotChanged += UpdateSlot;
        }
        else Debug.LogWarning($"No inventory assigned to {this.gameObject}");
        AssignSlot(primaryInventorySystem);
    }
    public override void AssignSlot(InventorySystem invToDisplay)
    {
        if (slotsUI.Length != primaryInventorySystem.InventorySize) { Debug.Log($"InventorySlot out of sync on {this.gameObject}"); }

        slotDict = new Dictionary<ItemSlot,ItemSlotUI >();
        for (int i = 0; i < primaryInventorySystem.InventorySize; i++)
        {
            slotDict.Add(primaryInventorySystem.InventorySlots[i],slotsUI[i ]);
            slotsUI[i].Init(primaryInventorySystem.InventorySlots[i]);
        }
    }
}