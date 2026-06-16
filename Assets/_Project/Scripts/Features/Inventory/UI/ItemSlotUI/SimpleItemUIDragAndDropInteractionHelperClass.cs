using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Kope.Core.ServiceLocator;

public class SimpleItemUIDragAndDropInteractionHelperClass {
	private readonly ItemDragDropManager mgr;

	public SimpleItemUIDragAndDropInteractionHelperClass() {
		if (SceneServiceLocator.Instance.TryGetService<ItemDragDropManager>(out var manager)) {
			this.mgr = manager;
		}
	}

	public void BeginDrag(ItemSlotUI itemUISlot, CanvasGroup canvasGroup, Canvas parentCanvas) {
		if (itemUISlot == null || itemUISlot.AssignedInventorySlot == null || itemUISlot.AssignedInventorySlot.ItemData == null || mgr == null) return;

		this.mgr.BeginDrag(itemUISlot, parentCanvas, itemUISlot.AssignedInventorySlot.ItemData.itemIcon);

		canvasGroup.alpha = 0.5f;
		canvasGroup.blocksRaycasts = false;
	}

	public void Drag(PointerEventData eventData) {
		if (this.mgr == null) return;
		this.mgr.UpdateDragPosition(eventData.position);
	}

	public void EndTheDrag(CanvasGroup canvasGroup, ItemSlotUI itemUISlot) {
		if (this.mgr == null) return;

		canvasGroup.alpha = 1f;
		canvasGroup.blocksRaycasts = true;

		this.mgr.EndDrag();

		if (itemUISlot != null && itemUISlot.ParentDisplay != null && itemUISlot.ParentDisplay.isActiveAndEnabled) {
			itemUISlot.ParentDisplay.RefreshSlot(itemUISlot.AssignedInventorySlot);
		}
	}

	public void Drop(ItemSlotUI targetUISlot) {
		if (this.mgr == null || this.mgr.SourceSlotUI == null ||
		this.mgr.SourceSlotUI.AssignedInventorySlot == null || targetUISlot == null) return;

		ItemSlotUI sourceUISlot = this.mgr.SourceSlotUI;
		ItemSlot draggedSlot = this.mgr.SourceSlotUI.AssignedInventorySlot;
		ItemSlot targetSlot = targetUISlot.AssignedInventorySlot;

		// Handle drop cases
		// this handles if the source and target are the same slot
		if (sourceUISlot == targetUISlot) {
			RefreshBothInventorySlot(sourceUISlot, targetUISlot);
		}
		//this handles if the target slot is empty
		else if (targetSlot.ItemData == null) {
			MoveAllToEmpty(targetUISlot, draggedSlot, sourceUISlot);
		}
		//this handles if the target slot has the same item as the dragged slot
		// also handles merging stacks and refreshing if no space and overflow
		else if (targetSlot.ItemData == draggedSlot.ItemData) {
			MergeOrRefresh(targetUISlot, draggedSlot, sourceUISlot);
		}
		//this handles if the target slot has a different item than the dragged slot
		else {
			SwapSlots(sourceUISlot, targetUISlot);
		}
		// End the drag operation
		mgr.EndDrag();
	}

	private void MergeOrRefresh(ItemSlotUI targetUISlot, ItemSlot draggedSlot, ItemSlotUI sourceUISlot) {
		bool enoughSpace = targetUISlot.AssignedInventorySlot.EnoughRoomLeftInTheStack(draggedSlot.StackCount, out int availableSpace);
		if (enoughSpace)
			AddAllToTarget(targetUISlot, draggedSlot, sourceUISlot);
		else if (availableSpace > 0)
			AddPartialToTarget(targetUISlot, draggedSlot, sourceUISlot, availableSpace);
		else
			RefreshBothInventorySlot(sourceUISlot, targetUISlot);
	}

	private void AddAllToTarget(ItemSlotUI targetUISlot, ItemSlot draggedSlot, ItemSlotUI sourceUISlot) {
		targetUISlot.AssignedInventorySlot.AddToStack(draggedSlot.StackCount);
		sourceUISlot.AssignedInventorySlot.ClearSlot();
		RefreshBothInventorySlot(sourceUISlot, targetUISlot);
	}

	private void AddPartialToTarget(ItemSlotUI targetUISlot, ItemSlot draggedSlot, ItemSlotUI sourceUISlot, int availableSpace) {
		targetUISlot.AssignedInventorySlot.AddToStack(availableSpace);
		sourceUISlot.AssignedInventorySlot.AddToStack(-availableSpace);
		RefreshBothInventorySlot(sourceUISlot, targetUISlot);
	}

	private void MoveAllToEmpty(ItemSlotUI targetUISlot, ItemSlot draggedSlot, ItemSlotUI sourceUISlot) {
		targetUISlot.AssignedInventorySlot.SetInventorySlot(draggedSlot.ItemData, draggedSlot.StackCount);
		sourceUISlot.AssignedInventorySlot.ClearSlot();
		RefreshBothInventorySlot(sourceUISlot, targetUISlot);
	}

	private void SwapSlots(ItemSlotUI sourceUISlot, ItemSlotUI targetUISlot) {
		// need to make deep copy of the source and target slot data to avoid reference issues when swapping
		// if someone goes to swap two slots and one of them is the source slot, if we don't
		// make a copy, we will end up with reference issues and both slots will end up
		// with the same data after the swap. the item in the dragged slot will end up in both the
		// source and target slot after the swap because they will reference the same ItemSlot data.
		// by making a copy, we can avoid this issue and ensure that the source and target slots
		// end up with the correct data after the swap.

		// these class are fairly small so the deep copy is not a big deal, if we were dealing 
		// with larger classes we would want to implement a more efficient way to handle this, 
		// but for our purposes this is sufficient and keeps the code simple and easy to understand.
		ItemSlot sourceCopy = new(sourceUISlot.AssignedInventorySlot.ItemData, sourceUISlot.AssignedInventorySlot.StackCount);
		ItemSlot targetCopy = new(targetUISlot.AssignedInventorySlot.ItemData, targetUISlot.AssignedInventorySlot.StackCount);

		sourceUISlot.AssignedInventorySlot.SetInventorySlot(targetCopy);
		targetUISlot.AssignedInventorySlot.SetInventorySlot(sourceCopy);

		RefreshBothInventorySlot(sourceUISlot, targetUISlot);
	}

	private void RefreshBothInventorySlot(ItemSlotUI sourceUISlot, ItemSlotUI targetUISlot) {
		if (sourceUISlot != null && sourceUISlot.ParentDisplay != null)
			sourceUISlot.ParentDisplay.RefreshSlot(sourceUISlot.AssignedInventorySlot);
		if (targetUISlot != null && targetUISlot.ParentDisplay != null)
			targetUISlot.ParentDisplay.RefreshSlot(targetUISlot.AssignedInventorySlot);
	}

	private bool IsDroppedOnUIElement() {
		PointerEventData pointerData = new(EventSystem.current) { position = Input.mousePosition };
		List<RaycastResult> raycastResults = new();
		EventSystem.current.RaycastAll(pointerData, raycastResults);
		return raycastResults.Count > 0;
	}
}
