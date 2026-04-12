using Kope.Core.CompilerServices;
using UnityEngine;
using Kope.Core.EntityComponentRegistry;
using Kope.Core.Sensor;
[RequireComponent(typeof(CircleCollider2D))]
public class PlayerItemCollector : SensorBase {
	[SerializeField] private EntityComponentsRegistry ecr;

	private InventoryHolder inventoryHolder;


	public override void OnStart() {
		if (this.ecr == null) {
			MyLogger.Error("No EntityComponentStore assigned to PlayerItemCollector" + this._parentGOHiearchPathMessage);
			return;
		}
		if (this.ecr.ComponentRegistry == null) {
			MyLogger.Error("The EntityComponentStore assigned to PlayerItemCollector does not have a valid ComponentRegistry" + this._parentGOHiearchPathMessage);
			return;
		}
		// since we are mutating the InventoryHolder by adding items to it, 
		// we need mutatable access here. so using TryGetMutatableComponent for semantic clarity
		if (this.ecr.ComponentRegistry.TryGetMutatableComponent<InventoryHolder>(out var invHolder)) {
			this.inventoryHolder = invHolder;
		} else {
			MyLogger.Error("No InventoryHolder found in EntityComponentStoreConfig for PlayerItemCollector" + this._parentGOHiearchPathMessage);
			return;
		}
	}

	public override void OnDetect(Collider2D other) {
		if (!other.TryGetComponent<ItemPickup>(out var itemPickup)) return;

		var inventory = inventoryHolder.PrimaryInventorySystem;
		if (inventory == null) return;

		if (inventory.AddToInventory(itemPickup.ItemData, itemPickup.StackCount)) {
			Destroy(other.gameObject);
		} else {
			MyLogger.Warn("Inventory full - cannot pick up item" + this._parentGOHiearchPathMessage);
		}
	}
}
