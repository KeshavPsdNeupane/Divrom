using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Written by chatGPT-4 deep model with some adjustments.
/// Simple manager to hold the currently dragged ItemSlot and source UI.
/// </summary>
public class DragDropManager : MonoBehaviour
{
    public static DragDropManager Instance { get; private set; }
    public ItemSlot CopyOfDraggedSourceItemSlot { get; private set; }
    public ItemSlotUI SourceSlotUI { get; private set; }

    private GameObject dragIcon;
    private Image dragIconImage;
    private Canvas parentCanvas;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this.gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(this.gameObject);
    }
    public void BeginDrag(ItemSlot slotCopy, ItemSlotUI sourceUI, Canvas canvas, Sprite icon)
    {
        this.CopyOfDraggedSourceItemSlot = slotCopy;
        this.SourceSlotUI = sourceUI;
        this.parentCanvas = canvas;

        if (this.dragIcon != null) Destroy(dragIcon);
        this.dragIcon = new GameObject("DragIcon");
        this.dragIcon.transform.SetParent(parentCanvas.transform, false);
        this.dragIconImage = dragIcon.AddComponent<Image>();
        this.dragIconImage.raycastTarget = false;
        this.dragIconImage.sprite = icon;
        this.dragIconImage.preserveAspect = true;
    }
    public void UpdateDragPosition(Vector2 screenPosition)
    {
        if (this.dragIcon != null)
            this.dragIcon.transform.position = screenPosition;
    }

    public void EndDrag()
    {
        if (this.dragIcon != null) Destroy(this.dragIcon);
        this.dragIcon = null;
        this.dragIconImage = null;
        this.CopyOfDraggedSourceItemSlot = null;
        this.SourceSlotUI = null;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }


    public bool IsDroppedItemIsOverUI(PointerEventData eventData)
    {
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        return results.Count > 0;
    }

}
