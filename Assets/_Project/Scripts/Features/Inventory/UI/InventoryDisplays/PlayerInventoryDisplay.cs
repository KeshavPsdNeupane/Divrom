using System.Collections.Generic;
using UnityEngine;
using Kope.Core.EntityComponentRegistry;

public class PlayerInventoryDisplayUI : InventoryDisplay {
	[SerializeField] private EntityComponentsRegistry ecr;
	[SerializeField] private GameObject slotPrefab;
	[SerializeField] private Transform slotParent;
	private InventoryHolder inventoryHolder;

	private readonly Queue<ItemSlotUI> slotPool = new();
	private bool prewarmed = false;

	protected override bool OnInit() {

		if (!base.OnInit()) return false; // impt if the base class is not InilializableBase
		if (this.slotPrefab == null) {
			Debug.LogError($"PlayerInventoryDisplayUI ({this.gameObject.name}): Slot Prefab is not assigned!" + this.HieararchyPath);
			return false;
		}
		if (this.slotParent == null) {
			Debug.LogError($"PlayerInventoryDisplayUI ({this.gameObject.name}): Slot Parent is not assigned!" + this.HieararchyPath);
			return false;
		}

		if (this.ecr == null) {
			Debug.LogError($"PlayerInventoryDisplayUI ({this.gameObject.name}): EntityComponentStore is not assigned!" + this.HieararchyPath);
			return false;
		}
		if (this.ecr.ComponentRegistry == null) {
			Debug.LogError($"PlayerInventoryDisplayUI ({this.gameObject.name}): ComponentRegistry in EntityComponentStore is null!" + this.HieararchyPath);
			return false;
		}
		// since we are mutating the InventoryHolder by adding items to it, 
		// we need mutatable access here. so using TryGetMutatableComponent for semantic clarity
		if (this.ecr.TryFetchMutable(this, this.HieararchyPath, out InventoryHolder invHolder)) {
			this.inventoryHolder = invHolder;
		} else {
			return false;
		}

		if (this.inventoryHolder.PrimaryInventorySystem == null) {
			Debug.LogError($"PlayerInventoryDisplayUI ({this.gameObject.name}): PrimaryInventorySystem is null in InventoryHolder!" + this.HieararchyPath);
			return false;
		}

		// Prewarm the slot pool to avoid runtime instantiation hitches
		// Dont have to check if inventory is assigned since we already did above
		if (!this.prewarmed)
			Prewarm();
		return true;
	}

	private void Prewarm() {
		var size = this.inventoryHolder.PrimaryInventorySystem.InventorySize;
		for (int i = 0; i < size; i++) {
			var go = Instantiate(this.slotPrefab, this.slotParent);
			var slotUI = go.GetComponent<ItemSlotUI>();
			slotUI.gameObject.SetActive(false);
			slotPool.Enqueue(slotUI);
		}
		this.prewarmed = true;
	}

	protected override void OnStart() {
		this.primaryInventorySystem = this.inventoryHolder.PrimaryInventorySystem;
		this.primaryInventorySystem.onInventoryShotChanged += UpdateSlot;
		AssignSlot(this.primaryInventorySystem);
	}

	public override void AssignSlot(InventorySystem invToDisplay) {
		if (this.slotDictionary != null) {
			foreach (var ui in this.slotDictionary.Values) {
				ui.gameObject.SetActive(false);
				this.slotPool.Enqueue(ui);
			}
		}
		this.slotDictionary = new Dictionary<ItemSlot, ItemSlotUI>();
		var invSize = this.primaryInventorySystem.InventorySize;
		for (int i = 0; i < invSize; i++) {
			ItemSlotUI slotUI;

			if (this.slotPool.Count > 0) {
				slotUI = this.slotPool.Dequeue();
			} else {
				var go = Instantiate(this.slotPrefab, this.slotParent);
				slotUI = go.GetComponent<ItemSlotUI>();
			}

			slotUI.transform.SetParent(this.slotParent, false);
			slotUI.gameObject.SetActive(true);
			slotUI.Init(this.primaryInventorySystem.InventorySlots[i]);

			this.slotDictionary.Add(this.primaryInventorySystem.InventorySlots[i], slotUI);
		}
	}
}
