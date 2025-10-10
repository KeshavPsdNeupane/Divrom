using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;
using System.Linq;

[System.Serializable]
public class InventorySystem
{
    [SerializeField] private List<ItemSlot> inventorySlots;

    public List<ItemSlot> InventorySlots => inventorySlots;
    public int InventorySize => inventorySlots.Count;

    public UnityAction<ItemSlot> onInventoryShotChanged;
    public InventorySystem(int size)
    {
        this.inventorySlots = new List<ItemSlot>(size);
        for (int i = 0; i < size; ++i)
        {
            this.inventorySlots.Add(new ItemSlot());
        }
    }

    public bool AddToInventory(ItemData itemToAdd, int amountToAdd)
    {
        if (ContainsItem(itemToAdd, out List<ItemSlot> invSlot))
        {
            foreach (ItemSlot slot in invSlot)
            {
                if (slot.EnoughRoomLeftInTheStack(amountToAdd))
                {
                    slot.AddToStack(amountToAdd);
                    onInventoryShotChanged?.Invoke(slot);
                    return true;

                }
            }
        }
        if (HasFreeSlot(out ItemSlot freeSlot))
        {
            freeSlot.SetInventorySlot(itemToAdd, amountToAdd);
            onInventoryShotChanged?.Invoke(freeSlot);
            return true;
        }
        return false;
    }

    public bool ContainsItem(ItemData itemToAdd, out List<ItemSlot> invSlot)
    {
        invSlot = this.inventorySlots.Where(shot => shot.ItemData == itemToAdd).ToList();
        return !(invSlot == null);
    }
    public bool HasFreeSlot(out ItemSlot freeSlot)
    {
        freeSlot = this.inventorySlots.FirstOrDefault(slot => slot.ItemData == null);
        return !(freeSlot == null);
    }
}
