using UnityEngine;
using Kope.Core.Init;
[System.Serializable]

public class InventoryHolder : InitializableBase
{
    [SerializeField] private int primaryInventorySize;
    [SerializeField] protected InventorySystem primaryInventorySystem;

    public InventorySystem PrimaryInventorySystem => primaryInventorySystem;

    //public static UnityAction<InventorySystem> onDynamicInventoryDisplayRequested;

    public override void Init()
    {
        if (this.IsInitialized) return;
        base.Init();
        primaryInventorySystem = new InventorySystem(this.primaryInventorySize);
    }

}
