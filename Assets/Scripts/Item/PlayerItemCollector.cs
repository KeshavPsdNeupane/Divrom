using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public class PlayerItemCollector : MonoBehaviour
{
    [SerializeField] private InventoryHolder inventoryHolder;
    [SerializeField] private CircleCollider2D detectionCollider;
    [SerializeField] private float detectionRadius = 1.5f;
    private void Awake()
    {
        if (this.detectionCollider == null)
            this.detectionCollider = GetComponent<CircleCollider2D>();

        if (this.detectionCollider != null)
        {
            this.detectionCollider.radius = this.detectionRadius;
            this.detectionCollider.isTrigger = true;
        }
        else
            Debug.LogError("No CircleCollider2D assigned to PlayerItemCollector", this);

        if (this.inventoryHolder == null)
            Debug.LogError("No InventoryHolder assigned to PlayerItemCollector", this);
    }


    private void OnTriggerEnter2D(Collider2D other)
    {
        var itemPickup = other.GetComponent<ItemPickup>();
        if (itemPickup == null) return;

        var inventory = this.inventoryHolder.PrimaryInventorySystem;
        if (inventory == null) return;

        if (inventory.AddToInventory(itemPickup.ItemData, itemPickup.StackCount))
        {
            Debug.Log($"Picked up {itemPickup.ItemData}x {itemPickup.ItemData.itemName}");
            Destroy(other.gameObject);
        }
        else
        {
            Debug.Log("Inventory full — cannot pick up item");
        }
    }
}
