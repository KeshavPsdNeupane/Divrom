using System.Collections.Generic;
using Kope.Core.CompilerServices;
using UnityEngine;

public class PlayerInventoryDisplayUI : InventoryDisplay
{
    [SerializeField] private InventoryHolder inventoryHolder;
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private Transform slotParent;

    private readonly Queue<ItemSlotUI> slotPool = new();
    private bool prewarmed = false;

    public override void Init()
    {
        if (this.IsInitialized) return;
        base.Init();
        if (this.inventoryHolder == null)
        {
            MyLogger.Error($"PlayerInventoryDisplayUI ({gameObject.name}): InventoryHolder is not assigned!");
            return;
        }

        if (this.inventoryHolder.PrimaryInventorySystem == null)
        {
            MyLogger.Error($"PlayerInventoryDisplayUI ({gameObject.name}): PrimaryInventorySystem is null! Make sure InventoryHolder is initialized first.");
            return;
        }

        if (this.slotPrefab != null && this.slotParent != null && !this.prewarmed)
            Prewarm();
    }

    private void Prewarm()
    {
        var size = this.inventoryHolder.PrimaryInventorySystem.InventorySize;
        for (int i = 0; i < size; i++)
        {
            var go = Instantiate(this.slotPrefab, this.slotParent);
            var slotUI = go.GetComponent<ItemSlotUI>();
            slotUI.gameObject.SetActive(false);
            slotPool.Enqueue(slotUI);
        }
        this.prewarmed = true;
    }

    protected override void Start()
    {
        this.primaryInventorySystem = this.inventoryHolder.PrimaryInventorySystem;
        this.primaryInventorySystem.onInventoryShotChanged += UpdateSlot;
        AssignSlot(this.primaryInventorySystem);
    }

    public override void AssignSlot(InventorySystem invToDisplay)
    {
        // Return active slots to pool
        if (this.slotDictionary != null)
        {
            foreach (var ui in this.slotDictionary.Values)
            {
                ui.gameObject.SetActive(false);
                this.slotPool.Enqueue(ui);
            }
        }
        this.slotDictionary = new Dictionary<ItemSlot, ItemSlotUI>();
        var invSize = this.primaryInventorySystem.InventorySize;
        for (int i = 0; i < invSize; i++)
        {
            ItemSlotUI slotUI;

            if (this.slotPool.Count > 0)
            {
                slotUI = this.slotPool.Dequeue();
            }
            else
            {
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
