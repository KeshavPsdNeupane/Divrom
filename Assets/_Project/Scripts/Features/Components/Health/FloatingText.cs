using System;
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

		private int _textSize = 5;
		private float _timer;
		private Color _currentColor;
		private Vector3 _initialScale;

		private event Action<GameObject> OnRelease;


		public void SubScribeToRelease(Action<GameObject> callback) {
			this.OnRelease += callback;
		}

		private void Awake() {
			this._initialScale = transform.localScale;

			if (this.textMeshPro == null)
				this.textMeshPro = GetComponent<TextMeshPro>();

			ApplySettings();
		}

		public void Initialize(string formattedNumber, Color color, int textSize, Vector3 position, Quaternion rotation) {
			this._timer = 0f;
			this._textSize = textSize;
			this.textMeshPro.text = formattedNumber;
			this.transform.SetPositionAndRotation(position, rotation);
			this._currentColor = color;
			this._currentColor.a = 1f;
			this.textMeshPro.color = this._currentColor;

			FitBoundsToText();

			// Randomize Position
			this.transform.position += new Vector3(
				UnityEngine.Random.Range(-randomizeIntensity.x, randomizeIntensity.x),
				UnityEngine.Random.Range(-randomizeIntensity.y, randomizeIntensity.y),
				UnityEngine.Random.Range(-randomizeIntensity.z, randomizeIntensity.z)
			);
		}

		private void ApplySettings() {
			if (this.textMeshPro == null) return;
			this.textMeshPro.alignment = TextAlignmentOptions.Center;
			int layerID = SortingLayer.NameToID(sortingLayerName);
			this.textMeshPro.sortingOrder = sortingOrder;

			if (SortingLayer.IsValid(layerID)) {
				this.textMeshPro.sortingLayerID = layerID;
			} else {
				this.textMeshPro.sortingLayerID = SortingLayer.NameToID("Default");
			}
		}

		private void FitBoundsToText() {
			if (!Application.isPlaying || this.textMeshPro == null) return;
			this.textMeshPro.fontSize = this._textSize;
			this.textMeshPro.ForceMeshUpdate();
			this.textMeshPro.rectTransform.sizeDelta = (Vector2)this.textMeshPro.textBounds.size
				+ this.backgroundPadding * 2f;
		}

		private void Update() {
			this._timer += Time.deltaTime;
			float progress = this._timer / this.duration;

			if (progress >= 1.0f) {
				this.OnRelease?.Invoke(this.gameObject);
				ClearState();
				return;
			}

			this.transform.position += Vector3.up * (this.moveSpeed * Time.deltaTime);
			this._currentColor.a = this.alphaCurve.Evaluate(progress);
			this.textMeshPro.color = this._currentColor;
			this.transform.localScale = this._initialScale * this.scaleCurve.Evaluate(progress);
		}
		private void ClearState() {
			if (this.textMeshPro != null) this.textMeshPro.text = string.Empty;
			this._timer = 0f;
			this.transform.localScale = this._initialScale;
			this.OnRelease = null;
		}
	}
}