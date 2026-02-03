using Kope.Core.CompilerServices;
using UnityEngine;
using Kope.Core.Init;
using Kope.Core.EntityComponentSystem;

[RequireComponent(typeof(CircleCollider2D))]
public class PlayerItemCollector : InitializableBase
{
    [SerializeField] private EntityComponentStore ecs;
    [SerializeField] private CircleCollider2D detectionCollider;
    [SerializeField] private float detectionRadius = 1.0f;
    private InventoryHolder inventoryHolder;

    public override void OnInit()
    {
        base.OnInit();

        if (this.detectionCollider == null)
        {
            MyLogger.Error("No CircleCollider2D assigned to PlayerItemCollector" + GetParentGameObjectStackTraceMessage());
            return;
        }
        if (ecs == null)
        {
            MyLogger.Error("No EntityComponentStore assigned to PlayerItemCollector" + GetParentGameObjectStackTraceMessage());
            return;
        }
        if (ecs.ComponentRegistry.TryGetComponent<InventoryHolder>(out var invHolder))
        {
            this.inventoryHolder = invHolder;
        }
        else
        {
            MyLogger.Error("No InventoryHolder found in EntityComponentStoreConfig for PlayerItemCollector" + GetParentGameObjectStackTraceMessage());
            return;
        }
        this.detectionCollider.isTrigger = true;
        Vector3 parentScale = transform.lossyScale;
        this.detectionCollider.radius = detectionRadius / Mathf.Max(parentScale.x, parentScale.y);
        this.detectionCollider.radius = this.detectionRadius;

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

#if UNITY_EDITOR
    [SerializeField] private bool showGizmos = false;
    void OnDrawGizmos()
    {
        if (!this.enabled || !this.showGizmos) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, this.detectionRadius);
    }
#endif
}
