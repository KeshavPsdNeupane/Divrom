using UnityEngine;
using UnityEngine.UI;
using Kope.Core.ServiceLocator;


public class ItemDragDropManager : SceneServiceBase {
	public ItemSlotUI SourceSlotUI { get; private set; }
	private Canvas parentCanvas;
	private Image _dragIconImage1;

	public void Start() {
		this._dragIconImage1 = new GameObject("DragInventoryItemIcon").AddComponent<Image>();
		this._dragIconImage1.raycastTarget = false;
		this._dragIconImage1.gameObject.SetActive(false);
		this._dragIconImage1.preserveAspect = true;
	}

	public void BeginDrag(ItemSlotUI sourceUI, Canvas canvas, Sprite icon) {
		this.SourceSlotUI = sourceUI;
		this.parentCanvas = canvas;

		this._dragIconImage1.gameObject.transform.SetParent(parentCanvas.transform, false);
		this._dragIconImage1.gameObject.SetActive(true);
		this._dragIconImage1.sprite = icon;

	}
	public void UpdateDragPosition(Vector2 screenPosition) {
		this._dragIconImage1.gameObject.transform.position = screenPosition;
	}

	public void EndDrag() {
		this._dragIconImage1.gameObject.SetActive(false);
		this._dragIconImage1.sprite = null;
		this.SourceSlotUI = null;
	}
}
