using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

public abstract class InventoryDisplay : MonoBehaviour
{
    protected InventorySystem primaryInventorySystem;
    protected Dictionary<ItemSlot, ItemSlotUI> slotDictionary;
    public InventorySystem PrimaryInventorySystem => this.primaryInventorySystem;
    public abstract void AssignSlot(InventorySystem invToDisplay);
    protected virtual void Start() { }

    protected virtual void UpdateSlot(ItemSlot updatedSlot)
    {
        if (updatedSlot == null || this.slotDictionary == null || !this.slotDictionary.ContainsKey(updatedSlot))
            return;
        this.slotDictionary[updatedSlot].UpdateUiSlot(updatedSlot);
    }

    public void RefreshSlot(ItemSlot slot)
    {
        UpdateSlot(slot);
    }
    public void RefreshAllSlot()
    {
       foreach (var slot in this.slotDictionary.Keys)
       {
            UpdateSlot(slot);
       }
    }

}
