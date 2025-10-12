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
[RequireComponent(typeof(ItemSlotDoubleTap))]
public class ItemSlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [Header("UI References")]
    [SerializeField] private Image itemSprite;
    [SerializeField] private TextMeshProUGUI itemCount;

    [Header("Inventory Slot")]
    [SerializeField] private ItemSlot assignedInventorySlot;

    [Header("Button Ref for Splitting")]
    [SerializeField] private ItemSlotDoubleTap splitActionButtonAssignment;

    public ItemSlot AssignedInventorySlot => assignedInventorySlot;

    public InventoryDisplay ParentDisplay { get; private set; }

    private Canvas parentCanvas;
    private CanvasGroup canvasGroup;

    private void Awake()
    {
        if (this.ParentDisplay == null)
            this.ParentDisplay = GetComponentInParent<InventoryDisplay>();

        this.parentCanvas = GetComponentInParent<Canvas>();
        this.canvasGroup = GetComponent<CanvasGroup>();
        if (this.canvasGroup == null) this.canvasGroup = this.gameObject.AddComponent<CanvasGroup>();
        // initial visuals
        UpdateUiSlot();
    }


    private void OnEnable()
    {
        if (splitActionButtonAssignment != null)
            splitActionButtonAssignment.onDoubleTapEvent += SplitItem;
    }

    private void OnDisable()
    {
        if (splitActionButtonAssignment != null)
            splitActionButtonAssignment.onDoubleTapEvent -= SplitItem;
    }

    public void Init(ItemSlot slot)
    {
        this.assignedInventorySlot = slot;
        UpdateUiSlot(slot);
    }

    public void UpdateUiSlot(ItemSlot slot)
    {
        if (slot != null && slot.ItemData != null && slot.ItemData.itemIcon != null)
        {
            this.itemSprite.sprite = slot.ItemData.itemIcon;
            this.itemSprite.color = Color.white;
            this.itemCount.text = slot.StackCount > 1 ? slot.StackCount.ToString() : "";
        }
        else
        {
            ClearSlot();
        }
    }

    public void UpdateUiSlot()
    {
        if (this.assignedInventorySlot != null)
            UpdateUiSlot(this.assignedInventorySlot);
    }

    public void ClearSlot()
    {
        this.itemSprite.sprite = null;
        this.itemSprite.color = Color.clear;
        this.itemCount.text = "";
    }

    #region Drag handlers
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (this.assignedInventorySlot == null || this.assignedInventorySlot.ItemData == null) return;

        // create a copy of the slot so the manager can work with it safely
        ItemSlot tempCopy = new ItemSlot(this.assignedInventorySlot.ItemData, this.assignedInventorySlot.StackCount);

        // ensure DragDropManager exists in scene
        if (DragDropManager.Instance == null)
        {
            GameObject go = new GameObject("DragDropManager");
            go.AddComponent<DragDropManager>();
        }

        DragDropManager.Instance.BeginDrag(tempCopy, this, this.parentCanvas, this.assignedInventorySlot.ItemData.itemIcon);

        // optional: visually hide the original while dragging (makes it obvious)
        this.canvasGroup.alpha = 0.5f;
        this.canvasGroup.blocksRaycasts = false; // so raycasts pass to drop targets
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (DragDropManager.Instance == null) return;
        DragDropManager.Instance.UpdateDragPosition(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // if nothing was dropped, just reset
        this.canvasGroup.alpha = 1f;
        this.canvasGroup.blocksRaycasts = true;

        // EndDrag always clears manager state to avoid stale references
        if (DragDropManager.Instance != null)
            DragDropManager.Instance.EndDrag();

        // Refresh this slot UI (in case it changed)
        this.ParentDisplay?.RefreshSlot(assignedInventorySlot);
    }
    #endregion

    #region Drop handling
    public void OnDrop(PointerEventData eventData)
    {
        // Get the manager and dragged slot
        var mgr = DragDropManager.Instance;
        if (mgr == null || mgr.CopyOfDraggedSourceItemSlot == null || mgr.SourceSlotUI == null) return;

        ItemSlot draggedSlot = mgr.CopyOfDraggedSourceItemSlot;   
        ItemSlotUI sourceUISlot = mgr.SourceSlotUI;    
        ItemSlot targetSlot = this.assignedInventorySlot;


        // If source and target are same UI, nothing to do
        if (sourceUISlot == this)
        {
            this.ParentDisplay?.RefreshSlot(assignedInventorySlot);
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
           // int availableSpace = targetSlot.ItemData.maxStackSize - targetSlot.StackCount;
            bool IsEnoughtSpaceAvailable = targetSlot.EnoughRoomLeftInTheStack(draggedSlot.StackCount, out int availableSpace);
            if (/*availableSpace >= draggedSlot.StackCount*/ IsEnoughtSpaceAvailable)
            {
                // can put all dragged into target
                targetSlot.AddToStack(draggedSlot.StackCount);
                sourceUISlot.assignedInventorySlot.ClearSlot();

                sourceUISlot.ParentDisplay?.RefreshSlot(sourceUISlot.assignedInventorySlot);
                this.ParentDisplay?.RefreshSlot(targetSlot);

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
               this.ParentDisplay?.RefreshSlot(targetSlot);
                mgr.EndDrag();
                return;
            }
        }

        // CASE 3: Different item types -> swap the contents
        // Create deep copies to avoid referencing the same ItemData incorrectly
        ItemSlot sourceCopy = new ItemSlot(sourceUISlot.assignedInventorySlot.ItemData, sourceUISlot.assignedInventorySlot.StackCount);
        ItemSlot targetCopy = new ItemSlot(targetSlot.ItemData, targetSlot.StackCount);

        // perform swap
        sourceUISlot.assignedInventorySlot.SetInventorySlot(targetCopy);
        targetSlot.SetInventorySlot(sourceCopy);

        sourceUISlot.ParentDisplay?.RefreshSlot(sourceUISlot.assignedInventorySlot);
        this.ParentDisplay?.RefreshSlot(targetSlot);
        mgr.EndDrag();
    }
    #endregion


    #region SplitItemStack
    public void SplitItem()
    {
        if (this.assignedInventorySlot == null || this.assignedInventorySlot.ItemData == null)
            return;

        int count = this.assignedInventorySlot.StackCount;
        if (count <= 1)
            return;

        var inventory = this.ParentDisplay?.PrimaryInventorySystem;
        bool hasFreeSlot = inventory.HasFreeSlot(out ItemSlot freeSlot);

        if (!hasFreeSlot) return;

        bool canSplit = this.assignedInventorySlot.SplitStack(out ItemSlot halfStack);
        
        if (!canSplit || halfStack == null) return;

        freeSlot.SetInventorySlot(halfStack);

        this.ParentDisplay?.RefreshSlot(assignedInventorySlot);
        this.ParentDisplay?.RefreshSlot(freeSlot);
    }

    #endregion



}
