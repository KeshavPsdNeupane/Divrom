// ItemSlotUI.cs
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Fully implemented drag/drop logic for single-slot UI:
/// - Begin drag: create DragDropManager copy and drag icon
/// - Drop on another ItemSlotUI: merge stacks (if same item), move to empty slot, or swap
/// - Handles UI refresh via ParentDisplay.RefreshSlot(...)
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class ItemSlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [Header("UI References")]
    [SerializeField] private Image itemSprite;
    [SerializeField] private TextMeshProUGUI itemCount;

    [Header("Inventory Slot")]
    [SerializeField] private ItemSlot assignedInventorySlot;
    public ItemSlot AssignedInventorySlot => assignedInventorySlot;

    [field: SerializeField] public InventoryDisplay ParentDisplay { get; private set; }

    private Canvas parentCanvas;
    private CanvasGroup canvasGroup;

    private void Awake()
    {
        if (ParentDisplay == null)
            ParentDisplay = GetComponentInParent<InventoryDisplay>();

        parentCanvas = GetComponentInParent<Canvas>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // initial visuals
        UpdateUiSlot();
    }

    public void Init(ItemSlot slot)
    {
        assignedInventorySlot = slot;
        UpdateUiSlot(slot);
    }

    public void UpdateUiSlot(ItemSlot slot)
    {
        if (slot != null && slot.ItemData != null && slot.ItemData.itemIcon != null)
        {
            itemSprite.sprite = slot.ItemData.itemIcon;
            itemSprite.color = Color.white;
            itemCount.text = slot.StackCount > 1 ? slot.StackCount.ToString() : "";
        }
        else
        {
            ClearSlot();
        }
    }

    public void UpdateUiSlot()
    {
        if (assignedInventorySlot != null)
            UpdateUiSlot(assignedInventorySlot);
    }

    public void ClearSlot()
    {
        itemSprite.sprite = null;
        itemSprite.color = Color.clear;
        itemCount.text = "";
    }

    #region Drag handlers
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (assignedInventorySlot == null || assignedInventorySlot.ItemData == null) return;

        // create a copy of the slot so the manager can work with it safely
        ItemSlot tempCopy = new ItemSlot(assignedInventorySlot.ItemData, assignedInventorySlot.StackCount);

        // ensure DragDropManager exists in scene
        if (DragDropManager.Instance == null)
        {
            GameObject go = new GameObject("DragDropManager");
            go.AddComponent<DragDropManager>();
        }

        DragDropManager.Instance.BeginDrag(tempCopy, this, parentCanvas, assignedInventorySlot.ItemData.itemIcon);

        // optional: visually hide the original while dragging (makes it obvious)
        canvasGroup.alpha = 0.5f;
        canvasGroup.blocksRaycasts = false; // so raycasts pass to drop targets
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (DragDropManager.Instance == null) return;
        DragDropManager.Instance.UpdateDragPosition(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // if nothing was dropped, just reset
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        // EndDrag always clears manager state to avoid stale references
        if (DragDropManager.Instance != null)
            DragDropManager.Instance.EndDrag();

        // Refresh this slot UI (in case it changed)
        ParentDisplay?.RefreshSlot(assignedInventorySlot);
    }
    #endregion

    #region Drop handling
    public void OnDrop(PointerEventData eventData)
    {
        // Get the manager and dragged slot
        var mgr = DragDropManager.Instance;
        if (mgr == null || mgr.DraggedSlot == null || mgr.SourceSlotUI == null) return;

        ItemSlot draggedSlot = mgr.DraggedSlot;         // what’s being dragged
        ItemSlotUI sourceUISlot = mgr.SourceSlotUI;     // where it came from
        ItemSlot targetSlot = this.assignedInventorySlot; // where it’s being dropped


        // If source and target are same UI, nothing to do
        if (sourceUISlot == this)
        {
            // no-op, but still ensure UI resets
            ParentDisplay?.RefreshSlot(assignedInventorySlot);
            return;
        }

        // CASE 1: Target empty -> move dragged into target, clear source
        if (targetSlot.ItemData == null)
        {
            targetSlot.SetInventorySlot(draggedSlot.ItemData, draggedSlot.StackCount);

            // clear source
            sourceUISlot.assignedInventorySlot.ClearSlot();
            // refresh both
            sourceUISlot.ParentDisplay?.RefreshSlot(sourceUISlot.assignedInventorySlot);
            ParentDisplay?.RefreshSlot(targetSlot);

            // finalize
            mgr.EndDrag();
            return;
        }

        // CASE 2: Same item type -> try to merge stacks
        if (targetSlot.ItemData == draggedSlot.ItemData)
        {
            int availableSpace = targetSlot.ItemData.maxStackSize - targetSlot.StackCount;
            if (availableSpace >= draggedSlot.StackCount)
            {
                // can put all dragged into target
                targetSlot.AddToStack(draggedSlot.StackCount);
                sourceUISlot.assignedInventorySlot.ClearSlot();

                sourceUISlot.ParentDisplay?.RefreshSlot(sourceUISlot.assignedInventorySlot);
                ParentDisplay?.RefreshSlot(targetSlot);

                mgr.EndDrag();
                return;
            }
            else if (availableSpace > 0)
            {
                // partially fill target, reduce source stack
                targetSlot.AddToStack(availableSpace);
                sourceUISlot.assignedInventorySlot.AddToStack(-availableSpace); // subtract from source

                sourceUISlot.ParentDisplay?.RefreshSlot(sourceUISlot.assignedInventorySlot);
                ParentDisplay?.RefreshSlot(targetSlot);

                mgr.EndDrag();
                return;
            }
            else
            {
                // target full - nothing changes
                sourceUISlot.ParentDisplay?.RefreshSlot(sourceUISlot.assignedInventorySlot);
                ParentDisplay?.RefreshSlot(targetSlot);
                mgr.EndDrag();
                return;
            }
        }

        // CASE 3: Different item types -> swap the contents
        // Create deep copies to avoid referencing the same ItemData incorrectly
        ItemSlot sourceCopy = new ItemSlot(sourceUISlot.assignedInventorySlot.ItemData, sourceUISlot.assignedInventorySlot.StackCount);
        ItemSlot targetCopy = new ItemSlot(targetSlot.ItemData, targetSlot.StackCount);

        // perform swap
        sourceUISlot.assignedInventorySlot.SetInventorySlot(targetCopy.ItemData, targetCopy.StackCount);
        targetSlot.SetInventorySlot(sourceCopy.ItemData, sourceCopy.StackCount);

        sourceUISlot.ParentDisplay?.RefreshSlot(sourceUISlot.assignedInventorySlot);
        ParentDisplay?.RefreshSlot(targetSlot);

        mgr.EndDrag();
    }
    #endregion
}
