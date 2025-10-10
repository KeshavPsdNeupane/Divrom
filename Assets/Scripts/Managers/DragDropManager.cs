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

    // The temporary dragged slot (a copy of the source slot content)
    public ItemSlot DraggedSlot { get; private set; }

    // The UI that originally provided the dragged slot
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

    /// <summary>
    /// Begin dragging: store a copy of the item data and the source UI,
    /// create a UI icon that follows the cursor.
    /// </summary>
    public void BeginDrag(ItemSlot slotCopy, ItemSlotUI sourceUI, Canvas canvas, Sprite icon)
    {
        this.DraggedSlot = slotCopy;
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

    /// <summary>
    /// Update drag icon position (call from OnDrag).
    /// </summary>
    public void UpdateDragPosition(Vector2 screenPosition)
    {
        if (dragIcon != null)
            dragIcon.transform.position = screenPosition;
    }

    /// <summary>
    /// End dragging: remove icon and clear the dragged references.
    /// </summary>
    public void EndDrag()
    {
        if (dragIcon != null) Destroy(dragIcon);
        dragIcon = null;
        dragIconImage = null;
        DraggedSlot = null;
        SourceSlotUI = null;
    }

    /// <summary>
    /// Clean only the dragged references (keeps icon), used if you want to temporarily release references.
    /// </summary>
    public void ClearDragged()
    {
        DraggedSlot = null;
        SourceSlotUI = null;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
