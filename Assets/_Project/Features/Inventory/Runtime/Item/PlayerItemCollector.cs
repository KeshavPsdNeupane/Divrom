using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public class PlayerItemCollector : InitializableBase
{
    [SerializeField] private InventoryHolder inventoryHolder;
    [SerializeField] private CircleCollider2D detectionCollider;
    [SerializeField] private float detectionRadius = 1.5f;

    public override void Init()
    {
        if (this.detectionCollider == null)
            this.detectionCollider = GetComponent<CircleCollider2D>();

        if (this.detectionCollider != null)
        {
            this.detectionCollider.radius = this.detectionRadius;
            this.detectionCollider.isTrigger = true;
        }
        else
            Logger.Error($"No CircleCollider2D assigned to PlayerItemCollector = {this}");

        if (this.inventoryHolder == null)
            Logger.Error($"No InventoryHolder assigned to PlayerItemCollector = {gameObject.name}");
        SetInitialized();
    }


    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent<ItemPickup>(out var itemPickup)) return;

        var inventory = this.inventoryHolder.PrimaryInventorySystem;
        if (inventory == null) return;

        if (inventory.AddToInventory(itemPickup.ItemData, itemPickup.StackCount))
        {
            Destroy(other.gameObject);
        }
        else
        {
            Logger.Warn("Inventory full – cannot pick up item");
        }
    }
}
