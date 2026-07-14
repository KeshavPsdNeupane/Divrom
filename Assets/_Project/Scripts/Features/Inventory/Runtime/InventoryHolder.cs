using UnityEngine;
using Kope.EntityComponentSystem;
[System.Serializable]

public class InventoryHolder : ComponentBase {
	[SerializeField] private int primaryInventorySize;

	// this is not a InitiazableBase so no need for the ECS reference
	[SerializeField] protected InventorySystem primaryInventorySystem;

	public InventorySystem PrimaryInventorySystem => primaryInventorySystem;

	//public static UnityAction<InventorySystem> onDynamicInventoryDisplayRequested;

	protected override bool OnInit() {
		this.primaryInventorySystem = new InventorySystem(this.primaryInventorySize);
		return true;
	}

}
