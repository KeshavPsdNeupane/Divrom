using Kope.Core.CompilerServices;
using UnityEngine;
using Kope.Core.Init;


[RequireComponent(typeof(CircleCollider2D))]
public class PlayerItemCollector : InitializableBase
{
    [SerializeField] private InventoryHolder inventoryHolder;
    [SerializeField] private CircleCollider2D detectionCollider;
    [SerializeField] private float detectionRadius = 1.0f;

    public override void Init()
    {
        if (detectionCollider == null)
            detectionCollider = GetComponent<CircleCollider2D>();

        if (detectionCollider != null)
        {
            // Adjust radius so it's in world scale (ignores parent scale)
            Vector3 parentScale = transform.lossyScale;
            detectionCollider.radius = detectionRadius / Mathf.Max(parentScale.x, parentScale.y);
            detectionCollider.isTrigger = true;
        }
        else
        {
            MyLogger.Error($"No CircleCollider2D assigned to PlayerItemCollector = {this}");
        }

        if (inventoryHolder == null)
            MyLogger.Error($"No InventoryHolder assigned to PlayerItemCollector = {gameObject.name}");

        SetInitialized();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent<ItemPickup>(out var itemPickup)) return;

        var inventory = inventoryHolder.PrimaryInventorySystem;
        if (inventory == null) return;

        if (inventory.AddToInventory(itemPickup.ItemData, itemPickup.StackCount))
        {
            Destroy(other.gameObject);
        }
        else
        {
            MyLogger.Warn("Inventory full – cannot pick up item");
        }
    }

    void OnDrawGizmos()
    {
        if (!this.enabled) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, this.detectionRadius);
    }
}
