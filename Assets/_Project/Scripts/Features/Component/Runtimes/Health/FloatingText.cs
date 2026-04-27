using Kope.Core.ObjectPooling;
using ServiceLocatorPattern;
using TMPro;
using UnityEngine;

namespace Kope.Component.Health {
	/// <summary>
	/// Handles the visual representation of floating combat text for 2D.
	/// Designed to be used with a TextMeshPro (Non-UGUI) component in World Space.
	/// </summary>
	[RequireComponent(typeof(TextMeshPro))]
	public class FloatingText : MonoBehaviour, IPoolable {
		/*
    Why does FloatingText manage its own lifetime and release?
    
    1. Feedback Autonomy: Much like a projectile, FloatingText is "fired" and then 
       becomes independent. The Spawner (CombatTextSpawner) should not be 
       burdened with tracking hundreds of text instances to see if they've faded out.
       
    2. Lifecycle Consistency: The text's life is defined by its visual duration. 
       By ticking its own timer and calling Release(this), the FloatingText 
       ensures it returns to the pool exactly when its animation ends, 
       decoupling the UI feedback layer from the combat logic layer.

    3. Performance: Handling the release internally allows the ObjectPooler 
       to treat FloatingText as a "fire-and-forget" service, which is essential 
       when high-frequency combat (like AOE or rapid fire) generates dozens 
       of instances per second.
*/
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
		private ObjectPooler _universalPooler;

		// --- IPoolable Implementation ---

		/// <summary>
		/// Automatically set by the PoolGroup during Preload/CreateNew.
		/// </summary>
		public GameObject OriginPrefab { get; set; }

		/// <summary>
		/// Resets the object state. Called by ObjectPooler.Release before returning to queue.
		/// </summary>
		public void ClearState() {
			if (this.textMeshPro != null) this.textMeshPro.text = string.Empty;
			this._timer = 0f;
			this.transform.localScale = this._initialScale;
		}

		// --------------------------------

		/*
        TODO: Potential optimizations and features to consider:
        - Material Variants: Use material property blocks to change text color without 
        creating new material instances.
        - Performance: Consider using a single mesh with multiple quads for text instead of individual 
        GameObjects for each text instance, if performance becomes an issue.
        */

		private void Awake() {
			this._initialScale = transform.localScale;

			if (this.textMeshPro == null)
				this.textMeshPro = GetComponent<TextMeshPro>();

			if (!GlobalServiceLocator.Instance.TryGetService(out this._universalPooler)) {
				Debug.LogError("FloatingText: Failed to get ObjectPooler from Service Locator.");
			}

			ApplySettings();
		}

		/// <summary>
		/// Initializes the floating text with the specified parameters.
		/// </summary>
		public void Initialize(string formattedNumber, Color color, int textSize, Vector3 position, Quaternion rotation) {
			this._textSize = textSize;
			this.textMeshPro.text = formattedNumber;
			this.transform.SetPositionAndRotation(position, rotation);
			this._currentColor = color;
			this._currentColor.a = 1f;
			this.textMeshPro.color = this._currentColor;

			FitBoundsToText();

			// Randomize Position
			this.transform.position += new Vector3(
				Random.Range(-randomizeIntensity.x, randomizeIntensity.x),
				Random.Range(-randomizeIntensity.y, randomizeIntensity.y),
				Random.Range(-randomizeIntensity.z, randomizeIntensity.z)
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
				if (this._universalPooler != null) {
					this._universalPooler.Release(this);
				} else {
					Destroy(this.gameObject);
				}
				return;
			}

			this.transform.position += Vector3.up * (this.moveSpeed * Time.deltaTime);
			this._currentColor.a = this.alphaCurve.Evaluate(progress);
			this.textMeshPro.color = this._currentColor;
			this.transform.localScale = this._initialScale * this.scaleCurve.Evaluate(progress);
		}
	}
}