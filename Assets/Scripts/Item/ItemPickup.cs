using UnityEngine;
public class ItemPickup : MonoBehaviour
{
    [SerializeField] private ItemData itemData;
    [SerializeField] private int stackCount = 1;
    public ItemData ItemData => this.itemData;
    public int StackCount => this.stackCount;
    public void SetItem(ItemData data, int count)
    {
        this.itemData = data;
        this.stackCount = count;
    }
}
