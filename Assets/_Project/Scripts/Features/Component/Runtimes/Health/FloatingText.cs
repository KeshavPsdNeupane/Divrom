using TMPro;
using UnityEngine;

namespace Kope.Component.Health {
	/// <summary>
	/// Handles the visual representation of floating combat text for 2D.
	/// Designed to be used with a TextMeshPro (Non-UGUI) component in World Space.
	/// </summary>
	[RequireComponent(typeof(TextMeshPro))]
	public class FloatingText : MonoBehaviour {
		[Header("Text Settings")]
		[SerializeField] private TextMeshPro textMeshPro;
		[SerializeField] private string sortingLayerName = "Default";
		[SerializeField] private int sortingOrder = 999;
		[SerializeField] private Vector2 backgroundPadding = new(0.5f, 0.25f);

		[Header("Movement Settings")]
		[SerializeField] private float moveSpeed = 1.5f;
		[SerializeField] private Vector3 randomizeIntensity = new(0.5f, 0f, 0f);

		[Header("Lifecycle")]
		[SerializeField] private float duration = 1.0f;
		[SerializeField] private AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
		[SerializeField] private AnimationCurve scaleCurve = AnimationCurve.Linear(0, 1, 1, 1.2f);
		private int textSize = 5;
		private float _timer;
		private Color _currentColor;
		private Vector3 _initialScale;

		/*
		TODO: Potential optimizations and features to consider:
		- Object Pooling: Implement a pooling system for floating text instances to reduce instantiation overhead.
		- Material Variants: Use material property blocks to change text color without 
		creating new material instances.
		- Performance: Consider using a single mesh with multiple quads for text instead of individual 
		GameObjects for each text instance, if performance becomes an issue with many floating texts.

		- Most Importantly, Object pooling should be implemented at the very least, as combat text 
		can be very frequent and cause performance issues if not managed properly.
		*/


		/// <summary>
		/// Initializes the floating text with the specified parameters. This method should be called 
		/// immediately after instantiating the floating text prefab.
		/// </summary>
		public void Initialize(string formattedNumber, Color color, int textSize) {

			// 1. Reset State
			this._timer = 0f;
			this.transform.localScale = this._initialScale;

			this.textSize = textSize;
			this.textMeshPro.text = formattedNumber;

			this._currentColor = color;
			this._currentColor.a = 1f;
			this.textMeshPro.color = this._currentColor;
			// actual fitting of the background to the text happens in FitBoundsToText, 
			// which is called after setting the text
			FitBoundsToText();

			// 4. Randomize Position (relative to spawn point)
			this.transform.localPosition += new Vector3(
				Random.Range(-randomizeIntensity.x, randomizeIntensity.x),
				Random.Range(-randomizeIntensity.y, randomizeIntensity.y),
				Random.Range(-randomizeIntensity.z, randomizeIntensity.z)
			);
		}

		private void Awake() {
			this._initialScale = transform.localScale;

			if (this.textMeshPro == null)
				this.textMeshPro = GetComponent<TextMeshPro>();
			ApplySettings();
		}

		private void ApplySettings() {

			if (this.textMeshPro == null) return;
			if (this.textMeshPro.text == "") this.textMeshPro.text = "0";
			this.textMeshPro.alignment = TextAlignmentOptions.Center;
			int layerID = SortingLayer.NameToID(sortingLayerName);
			this.textMeshPro.sortingOrder = sortingOrder;
			if (SortingLayer.IsValid(layerID)) {
				this.textMeshPro.sortingLayerID = layerID;
			} else {
				Debug.LogWarning($"Sorting Layer '{sortingLayerName}' not found. Defaulting to 'Default' layer.");
				this.textMeshPro.sortingLayerID = SortingLayer.NameToID("Default");
			}
		}

		private void FitBoundsToText() {
			if (!Application.isPlaying || this.textMeshPro == null) return;
			this.textMeshPro.fontSize = this.textSize;
			this.textMeshPro.ForceMeshUpdate();
			this.textMeshPro.rectTransform.sizeDelta = this.textMeshPro.textBounds.size
				+ (Vector3)this.backgroundPadding * 2f;
		}

		private void Update() {
			this._timer += Time.deltaTime;
			float progress = this._timer / this.duration;

			if (progress >= 1.0f) {
				Destroy(this.gameObject);
				return;
			}

			this.transform.position += Vector3.up * (this.moveSpeed * Time.deltaTime);

			this._currentColor.a = this.alphaCurve.Evaluate(progress);
			this.textMeshPro.color = this._currentColor;

			this.transform.localScale = this._initialScale * this.scaleCurve.Evaluate(progress);
		}
	}
}