using UnityEngine;

[System.Serializable]
public class ItemSlot
{
    [SerializeField] private ItemData itemData;
    [SerializeField] private int stackCount;

    public ItemData ItemData => itemData;
    public int StackCount => stackCount;

    public ItemSlot(ItemData source, int amount) { this.itemData = source; this.stackCount = amount; }
    public ItemSlot() { ClearSlot(); }

    public bool EnoughRoomLeftInTheStack(int amountToAdd, out int amountRemaning)
    {
        amountRemaning = this.itemData.maxStackSize - stackCount;
        return EnoughRoomLeftInTheStack(amountToAdd);
    }
    public bool EnoughRoomLeftInTheStack(int amountToAdd)
    {
        if (this.stackCount + amountToAdd <= this.itemData.maxStackSize) { return true; }
        return false;

    }

    public void SetInventorySlot(ItemData data, int amount)
    {
        this.itemData = data;
        this.stackCount = amount;
    }

    public void ClearSlot()
    {
        this.itemData = null;
        this.stackCount = -1;
    }

    public void AssignItem(ItemSlot invSlot)
    {
        if (this.itemData == invSlot.itemData) { AddToStack(invSlot.StackCount); }
        else
        {
            this.itemData = invSlot.itemData;
            this.stackCount = 0;
            AddToStack(invSlot.stackCount);
        }

    }
    public void AddToStack(int amount)
    {
        stackCount += amount; //todo - need to add above func here 
    }


    public bool SplitStack(out ItemSlot halfStackSlot)
    {
        if (stackCount <= 1)
        {
            halfStackSlot = null;
            return false;
        }
        int halfStack = Mathf.RoundToInt(stackCount / 2);
        this.stackCount -= halfStack;
        halfStackSlot = new ItemSlot(this.itemData, halfStack);
        return true;

    }
}
