using UnityEngine;
using UnityEngine.EventSystems;

public class SimpleItemUIDragAndDropInteractionHelperClass
{
    public void BeginDrag(ItemSlotUI itemUISlot, CanvasGroup canvasGroup, Canvas parentCanvas)
    {
        if (itemUISlot == null || itemUISlot.AssignedInventorySlot == null || itemUISlot.AssignedInventorySlot.ItemData == null) return;
        ItemSlot tempCopy = new ItemSlot(itemUISlot.AssignedInventorySlot.ItemData, itemUISlot.AssignedInventorySlot.StackCount);
        if (DragDropManager.Instance == null)
        {
            GameObject go = new GameObject("DragDropManager");
            go.AddComponent<DragDropManager>();
        }
        DragDropManager.Instance.BeginDrag(tempCopy, itemUISlot, parentCanvas, itemUISlot.AssignedInventorySlot.ItemData.itemIcon);
        canvasGroup.alpha = 0.5f;
        canvasGroup.blocksRaycasts = false;
    }

    public void Drag(PointerEventData eventData)
    {
        if (DragDropManager.Instance == null) return;
        DragDropManager.Instance.UpdateDragPosition(eventData.position);
    }

    public void EndTheDrag(CanvasGroup canvasGroup, ItemSlotUI itemUISlot)
    {
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        if (DragDropManager.Instance != null)
            DragDropManager.Instance.EndDrag();

        if (itemUISlot.ParentDisplay != null && itemUISlot.AssignedInventorySlot != null && itemUISlot.ParentDisplay.isActiveAndEnabled)
        {
            itemUISlot.ParentDisplay.RefreshSlot(itemUISlot.AssignedInventorySlot);
        }
    }


    public void Drop(ItemSlotUI targetUISlot)
    {
        var mgr = DragDropManager.Instance;
        if (mgr == null || mgr.CopyOfDraggedSourceItemSlot == null || mgr.SourceSlotUI == null) return;

        ItemSlot draggedSlot = mgr.CopyOfDraggedSourceItemSlot;
        ItemSlotUI sourceUISlot = mgr.SourceSlotUI;
        ItemSlot targetSlot = targetUISlot.AssignedInventorySlot;


        if (sourceUISlot == targetUISlot)
        {
            targetUISlot.ParentDisplay?.RefreshSlot(targetSlot);
            mgr.EndDrag();
            return;
        }

        // CASE 1: Target empty -> move dragged into target, clear source
        if (targetSlot.ItemData == null)
        {
            TargetIsEmptyMovingAllItemAndClearTheScource(targetUISlot, draggedSlot, sourceUISlot);
            mgr.EndDrag();
            return;
        }

        // CASE 2: Same item type -> try to merge stacks
        if (targetSlot.ItemData == draggedSlot.ItemData)
        {
            bool IsEnoughtSpaceAvailable = targetSlot.EnoughRoomLeftInTheStack(draggedSlot.StackCount, out int availableSpace);
            if (IsEnoughtSpaceAvailable)
            {
                SameItemAndCanMergeFullyOnTargetSlot(targetUISlot, draggedSlot, sourceUISlot);
                mgr.EndDrag();
                return;
            }
            else if (availableSpace > 0)
            {
               SameItemButCannotFullyMergeOnTargetSlot(targetUISlot, draggedSlot, sourceUISlot, availableSpace);
                mgr.EndDrag();
                return;
            }
            else
            {
                // target full - nothing changes
               RefreshBothInventorySlot(sourceUISlot, targetUISlot);
                mgr.EndDrag();
                return;
            }
        }
        // CASE 3: Different item types -> swap the contents
        SwapItemSlot(sourceUISlot, targetUISlot);
        mgr.EndDrag();
    }





    public void TargetIsEmptyMovingAllItemAndClearTheScource(ItemSlotUI targetUISlot, ItemSlot draggedSlot, ItemSlotUI sourceUISlot)
    {
        targetUISlot.AssignedInventorySlot.SetInventorySlot(draggedSlot.ItemData, draggedSlot.StackCount);
        sourceUISlot.AssignedInventorySlot.ClearSlot();
        RefreshBothInventorySlot(sourceUISlot, targetUISlot);
    }

    public void SameItemAndCanMergeFullyOnTargetSlot(ItemSlotUI targetUISlot, ItemSlot draggedSlot, ItemSlotUI sourceUISlot)
    {
        targetUISlot.AssignedInventorySlot.AddToStack(draggedSlot.StackCount);
        sourceUISlot.AssignedInventorySlot.ClearSlot();
        RefreshBothInventorySlot(sourceUISlot, targetUISlot);

    }

    public void SameItemButCannotFullyMergeOnTargetSlot(
        ItemSlotUI targetUISlot, ItemSlot draggedSlot, ItemSlotUI sourceUISlot, int availableSpace)
    {
        targetUISlot.AssignedInventorySlot.AddToStack(availableSpace);
        sourceUISlot.AssignedInventorySlot.AddToStack(-availableSpace); // subtract from source
        RefreshBothInventorySlot(sourceUISlot, targetUISlot);
    }

    public void RefreshBothInventorySlot(ItemSlotUI sourceUISlot, ItemSlotUI targetUISlot)
    {
        sourceUISlot.ParentDisplay?.RefreshSlot(sourceUISlot.AssignedInventorySlot);
        targetUISlot.ParentDisplay?.RefreshSlot(targetUISlot.AssignedInventorySlot);
    }

    public void SwapItemSlot(ItemSlotUI sourceUISlot, ItemSlotUI targetUISlot)
    {
        // Create deep copies to avoid referencing the same ItemData incorrectly
        ItemSlot sourceCopy = new ItemSlot(sourceUISlot.AssignedInventorySlot.ItemData, sourceUISlot.AssignedInventorySlot.StackCount);
        ItemSlot targetCopy = new ItemSlot(targetUISlot.AssignedInventorySlot.ItemData, targetUISlot.AssignedInventorySlot.StackCount);
        sourceUISlot.AssignedInventorySlot.SetInventorySlot(targetCopy);
        targetUISlot.AssignedInventorySlot.SetInventorySlot(sourceCopy);
        RefreshBothInventorySlot(sourceUISlot, targetUISlot);
    }

}
