using System.Collections.Generic;
using Kope.Core.LifeTimeManagement;

public abstract class InventoryDisplay : InitializableBase {
	protected InventorySystem primaryInventorySystem;
	protected Dictionary<ItemSlot, ItemSlotUI> slotDictionary;
	public InventorySystem PrimaryInventorySystem => this.primaryInventorySystem;
	public abstract void AssignSlot(InventorySystem invToDisplay);

	/// <summary>
	/// Overrides the base <c>Initializable</c> lifecycle method to establish <c>OnStart</c> 
	/// as the specific start-up function for children of this class. 
	/// Subclasses can override this method to implement their own custom initialization.
	/// </summary>
	protected override void OnStart() { }

	protected virtual void UpdateSlot(ItemSlot updatedSlot) {
		if (updatedSlot == null || this.slotDictionary == null || !this.slotDictionary.ContainsKey(updatedSlot))
			return;
		this.slotDictionary[updatedSlot].UpdateUiSlot(updatedSlot);
	}

	public void RefreshSlot(ItemSlot slot) {
		UpdateSlot(slot);
	}
	public void RefreshAllSlot() {
		foreach (var slot in this.slotDictionary.Keys) {
			UpdateSlot(slot);
		}
	}

}
