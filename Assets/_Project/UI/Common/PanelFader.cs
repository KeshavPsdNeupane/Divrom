using Kope.Core.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Utility to immediately fade a UI panel (or any Graphic) to fully transparent.
/// </summary>
public class PanelFader : MonoBehaviour
{
    private void Awake()
    {
        // Try to get an Image or any Graphic component
        if (!this.gameObject.TryGetComponent<Graphic>(out var graphic))
            graphic = this.gameObject.GetComponentInChildren<Graphic>();

        if (graphic != null)
        {
            Color c = graphic.color;
            c.a = 0f;
            graphic.color = c;
        }
        else
        {
            MyLogger.Warn($"{gameObject.name} has no Graphic component to fade.");
        }
    }
}
