using UnityEngine;
using UnityEngine.UI;
using System;

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

        if (dragIcon != null) Destroy(dragIcon);
        dragIcon = new GameObject("DragIcon");
        dragIcon.transform.SetParent(parentCanvas.transform, false);
        dragIconImage = dragIcon.AddComponent<Image>();
        dragIconImage.raycastTarget = false;
        dragIconImage.sprite = icon;
        dragIconImage.preserveAspect = true;
    }
    public void UpdateDragPosition(Vector2 screenPosition)
    {
        if (dragIcon != null)
            dragIcon.transform.position = screenPosition;
    }

    public void EndDrag()
    {
        if (dragIcon != null) Destroy(dragIcon);
        dragIcon = null;
        dragIconImage = null;
        CopyOfDraggedSourceItemSlot = null;
        SourceSlotUI = null;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
