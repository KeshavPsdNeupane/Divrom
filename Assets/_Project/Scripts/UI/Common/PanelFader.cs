using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Utility to immediately fade a UI panel (or any Graphic) to fully transparent.
/// </summary>
public class PanelFader : MonoBehaviour {
	private void Awake() {
		// Try to get an Image or any Graphic component
		// this is unity TryGetComponent, we are not using the custom TryGetComponent extension method here,
		// since we are not trying to get a component from an EntityComponentStore,
		//  but rather from the GameObject itself. so using the standard Unity TryGetComponent is appropriate here.
		if (!this.gameObject.TryGetComponent<Graphic>(out var graphic))
			graphic = this.gameObject.GetComponentInChildren<Graphic>();

		if (graphic != null) {
			Color c = graphic.color;
			c.a = 0f;
			graphic.color = c;
		} else {
			Debug.LogWarning($"{gameObject.name} has no Graphic component to fade.");
		}
	}
}
