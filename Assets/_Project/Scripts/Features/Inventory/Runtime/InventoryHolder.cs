using UnityEngine;
using Kope.Core.Init;
[System.Serializable]

public class InventoryHolder : InitializableBase
{
	[SerializeField] private int primaryInventorySize;

	// this is not a InitiazableBase so no need for the ECS reference
	[SerializeField] protected InventorySystem primaryInventorySystem;

	public InventorySystem PrimaryInventorySystem => primaryInventorySystem;

	//public static UnityAction<InventorySystem> onDynamicInventoryDisplayRequested;

	public override bool OnInit()
	{
		this.primaryInventorySystem = new InventorySystem(this.primaryInventorySize);
		return true;
	}

}
