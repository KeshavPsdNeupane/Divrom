using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

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
    private SimpleItemUIDragAndDropInteractionHelperClass simpleDragDropHelperClass;

    private void Awake()
    {
        if (this.ParentDisplay == null)
            this.ParentDisplay = GetComponentInParent<InventoryDisplay>();

        this.parentCanvas = GetComponentInParent<Canvas>();
        this.canvasGroup = GetComponent<CanvasGroup>();
        this.simpleDragDropHelperClass = new SimpleItemUIDragAndDropInteractionHelperClass();
        if (this.canvasGroup == null) this.canvasGroup = this.gameObject.AddComponent<CanvasGroup>();
        UpdateUiSlot();
    }


    private void OnEnable()
    {
        if (this.splitActionButtonAssignment != null)
            this.splitActionButtonAssignment.onDoubleTapEvent += SplitItem;
    }

    private void OnDisable()
    {
        if (this.splitActionButtonAssignment != null)
            splitActionButtonAssignment.onDoubleTapEvent -= SplitItem;

        if (this.simpleDragDropHelperClass != null && this.canvasGroup != null)
        {   
            this.simpleDragDropHelperClass.EndTheDrag(this.canvasGroup, this);
        }   
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
        this.simpleDragDropHelperClass.BeginDrag(this, this.canvasGroup, this.parentCanvas);
    }
    public void OnDrag(PointerEventData eventData)
    {
        this.simpleDragDropHelperClass.Drag(eventData);
    }
    public void OnEndDrag(PointerEventData eventData)
    {
        this.simpleDragDropHelperClass.EndTheDrag(this.canvasGroup,this);
    }
    #endregion

    #region Drop handling
    public void OnDrop(PointerEventData eventData)
    {
        this.simpleDragDropHelperClass.Drop(this);
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

