using System;
using System.Collections.Generic;
using Kope.Component.Combat.Interface;
using Kope.Component.HitBox.Interface;
using Kope.Core;
using Kope.Core.ObjectPooling;
using ServiceLocatorPattern;
using UnityEngine;

namespace Kope.Component.Ability.Targeting {

	[Serializable]
	public sealed class AreaTargetingStrategyFactory : ITargetingFactory {
		[SerializeField] private bool includeCaster = false;
		[SerializeField] private GameObject previewPrefab;
		[SerializeField, Min(0.001f)] private float radius = 5f;
		[SerializeField] private LayerMask layerMask = 1;

		[SerializeField] private float previewHeightOffset = 0.1f;

		[SerializeField] private AxisMode dimension = AxisMode.TwoD;

		[Header("2D Only Settings")]
		[SerializeField, Min(-10), Tooltip("Minimum search depth for overlap checks. Only for 2D physics. Ignored for 3D physics.")]
		private int minSearchDepth = -1;
		[Header("Shared Settings")]
		[SerializeField, Range(1, 64)] private int maxTargets = 16;

		public TargetingStrategy Create() {
			if (this.dimension == AxisMode.TwoD)
				return new AreaTargetingStrategy2D(this.includeCaster, this.previewPrefab,
					this.radius, this.layerMask, this.previewHeightOffset,
					this.maxTargets, this.minSearchDepth);

			return new AreaTargetingStrategy3D(this.includeCaster, this.previewPrefab,
				this.radius, this.layerMask, this.previewHeightOffset, this.maxTargets);
		}
	}

	[Serializable]
	public abstract class AreaTargetingStrategy : TargetingStrategy {
		// Why does the Strategy manage the lifecycle of the Area Preview directly instead of 
		// letting the Preview release itself?
		//
		// Unlike projectiles, Area Previews are "Subordinate Objects." They have no independent 
		// behavior; they exist only as a visual extension of the Targeting Strategy. 
		//
		// Their lifecycle is strictly bound to the user's input—if the user cancels targeting 
		// or clicks to confirm, the preview must vanish instantly. Because the Strategy already 
		// has an active 'Update' loop to move the preview to the mouse position, it is the 
		// most efficient place to handle the 'Rent' and 'Release' calls. This ensures the 
		// preview never outlives the targeting phase and doesn't require its own 
		// independent ticking logic.
		protected readonly bool _includeCaster;
		protected readonly GameObject _previewPrefab;
		protected readonly float _radius;
		protected readonly LayerMask _layerMask;
		protected readonly float _previewHeightOffset;
		protected readonly int _maxTargets;

		private GameObject _previewInstance;
		private AbilityAreaTargetingController _previewController;
		private ObjectPooler _universalPooler; // Use the service instead of manual management

		protected Vector3 currentPoint;

		protected AreaTargetingStrategy(bool includeCaster, GameObject previewPrefab,
			float radius, LayerMask layerMask, float previewHeightOffset, int maxTargets) {
			this._includeCaster = includeCaster;
			this._previewPrefab = previewPrefab;
			this._radius = radius;
			this._layerMask = layerMask;
			this._previewHeightOffset = previewHeightOffset;
			this._maxTargets = maxTargets;
		}

		public override void Start(
			TargetingManager targetingManager,
			TargetContext casterContext,
			EffectContext effectContext,
			ITargetingReceiver onTargetResolved) {

			Begin(targetingManager, casterContext, effectContext, onTargetResolved);

			// Get the pooler service
			if (_universalPooler == null) {
				GlobalServiceLocator.Instance.TryGetService(out _universalPooler);
			}

			// Rent preview instead of Instantiate
			if (this._previewPrefab != null && this._universalPooler != null) {
				if (this._universalPooler.TryRent(this._previewPrefab, out this._previewInstance)) {
					this._previewController = this._previewInstance.GetComponent<AbilityAreaTargetingController>();

					if (this._previewController != null) {
						if (!this.targetingManager.TryGetMouseGroundPoint(out Vector3 mousePoint)) {
							mousePoint = Vector3.zero;
						}
						this._previewController.Initialize(mousePoint, this._radius);
					}
				}
			}
		}

		public override void Update() {
			if (!this._isTargeting || this.targetingManager == null) return;
			if (!this.targetingManager.TryGetMouseGroundPoint(out this.currentPoint)) return;
			if (this._previewController != null)
				UpdatePreviewPosition(this._previewController);
		}

		public override void FinishTheStratrgy(bool clearOnTargetResolved = true) {
			base.FinishTheStratrgy(clearOnTargetResolved);
			if (this._previewController != null && this._universalPooler != null) {
				this._universalPooler.Release(this._previewController);
			} else if (this._previewInstance != null) {
				UnityEngine.Object.Destroy(this._previewInstance);
			}
			this._previewInstance = null;
			this._previewController = null;
			ClearBuffers();
		}

		protected override bool ExecuteResolution(Vector3 clickPoint) {
			var targets = GetTargetsInArea(clickPoint);
			if (targets.Length > 0)
				ResolveGroupOfTargets(targets, clickPoint);
			return true;
		}

		protected void ProcessHit(UnityEngine.Component col,
			HashSet<IHitBoxComponent> uniqueHits, List<TargetContext> results) {
			var ctx = TargetContext.Create(col);
			if (ctx?.HitBox != null && uniqueHits.Add(ctx.HitBox))
				if (this._includeCaster || ctx.HitBox != this.casterContext.HitBox)
					results.Add(ctx);
		}

		protected abstract void UpdatePreviewPosition(AbilityAreaTargetingController controller);
		protected abstract TargetContext[] GetTargetsInArea(Vector3 point);
		protected abstract void ClearBuffers();
	}
}