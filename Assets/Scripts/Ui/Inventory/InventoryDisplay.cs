using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

public abstract class InventoryDisplay : MonoBehaviour
{
    protected InventorySystem primaryInventorySystem;
    protected Dictionary<ItemSlot, ItemSlotUI> slotDict;

    private ItemSlotUI draggedSlotUI;
    private GameObject dragIcon;


    public Dictionary<ItemSlot, ItemSlotUI> SlotDict =>slotDict;
    public InventorySystem PrimaryInventorySystem => this.primaryInventorySystem;
    public abstract void AssignSlot(InventorySystem invToDisplay);

    protected virtual void Start() { }

    protected virtual void UpdateSlot(ItemSlot updatedSlot)
    {
        this.slotDict[updatedSlot].UpdateUiSlot(updatedSlot);
    }
    public void RefreshSlot(ItemSlot slot)
    {
        UpdateSlot(slot);
    }

}
