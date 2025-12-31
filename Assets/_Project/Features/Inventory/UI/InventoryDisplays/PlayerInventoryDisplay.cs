using System.Collections.Generic;
using UnityEngine;

public class PlayerInventoryDisplayUI : InventoryDisplay
{
    [SerializeField] private InventoryHolder inventoryHolder;
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private Transform slotParent;

    public override void Init()
    {
        if (inventoryHolder == null)
        {
            Logger.Error($"PlayerInventoryDisplayUI ({gameObject.name}):" +
             " InventoryHolder is not assigned!");
            return;
        }

        if (inventoryHolder.PrimaryInventorySystem == null)
        {
            Logger.Error($"PlayerInventoryDisplayUI ({gameObject.name}):" +
             " InventoryHolder.PrimaryInventorySystem is null! Make " +
             "sure InventoryHolder is initialized before PlayerInventoryDisplayUI" +
             " in the InitLifecycleManager.");
            return;
        }

    }

    protected override void Start()
    {
        this.primaryInventorySystem = inventoryHolder.PrimaryInventorySystem;
        this.primaryInventorySystem.onInventoryShotChanged += UpdateSlot;
        AssignSlot(this.primaryInventorySystem);
    }


    public override void AssignSlot(InventorySystem invToDisplay)
    {
        if (this.slotParent != null)
        {
            foreach (Transform child in slotParent)
                Destroy(child.gameObject);
        }
        slotDictionary = new Dictionary<ItemSlot, ItemSlotUI>();

        for (int i = 0; i < this.primaryInventorySystem.InventorySize; i++)
        {
            GameObject newSlotObj = Instantiate(slotPrefab, slotParent ?? transform);
            ItemSlotUI slotUI = newSlotObj.GetComponent<ItemSlotUI>();

            slotUI.Init(this.primaryInventorySystem.InventorySlots[i]);
            slotDictionary.Add(this.primaryInventorySystem.InventorySlots[i], slotUI);
        }
    }
}
