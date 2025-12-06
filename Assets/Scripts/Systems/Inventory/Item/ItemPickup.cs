using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class ItemPickup : MonoBehaviour
{
    [SerializeField] private ItemData itemData;
    [SerializeField] private int stackCount = 1;
    [SerializeField] private SpriteRenderer spriteRenderer;
    public ItemData ItemData => this.itemData;
    public int StackCount => this.stackCount;

    private void Awake()
    {
        if (this.spriteRenderer == null)
            this.spriteRenderer = this.GetComponentInChildren<SpriteRenderer>();

        UpdateSprite();
    }

    private void OnValidate()
    {
        if (this.spriteRenderer == null)
            this.spriteRenderer = this.GetComponentInChildren<SpriteRenderer>();

        UpdateSprite();
    }

    private void UpdateSprite()
    {
        if (this.itemData != null && this.spriteRenderer != null)
            this.spriteRenderer.sprite = this.itemData.itemIcon;
    }

    public void SetItem(ItemData data, int count)
    {
        this.itemData = data;
        this.stackCount = count;
        UpdateSprite();
    }
}
