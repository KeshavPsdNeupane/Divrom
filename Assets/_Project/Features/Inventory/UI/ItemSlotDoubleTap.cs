using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;

[RequireComponent(typeof(Button))]
public class ItemSlotDoubleTap : MonoBehaviour, IPointerDownHandler
{
    public float doubleTapThreshold = 0.3f;
    private float lastTapTime = -1f;

    [SerializeField] private Button button;
    [SerializeField] public UnityAction onSingleTapEvent;
    [SerializeField] public UnityAction onDoubleTapEvent;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnSingleTap);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (Time.time - lastTapTime <= doubleTapThreshold)
        {
            OnDoubleTap();
            lastTapTime = -1f;
        }
        else
        {
            lastTapTime = Time.time;
        }
    }

    private void OnSingleTap()
    {
        this.onSingleTapEvent?.Invoke();
    }

    private void OnDoubleTap()
    {
        this.onDoubleTapEvent?.Invoke();
    }
}
