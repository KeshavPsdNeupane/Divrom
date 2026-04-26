using System;
using System.Collections.Generic;
using Kope.Component.Combat.Interface;
using UnityEngine;
using Kope.Component.HitBox.Interface;
using Kope.Core;

namespace Kope.Component.Ability.Targeting {
	[Serializable]
	public sealed class AreaTargetingStrategyFactory : ITargetingFactory {
		[SerializeField] private bool includeCaster = false;
		[SerializeField] private GameObject previewPrefab;
		[SerializeField, Min(0.001f)] private float radius = 5f;
		[SerializeField] private LayerMask layerMask = 1;
		[SerializeField, Min(-10), Tooltip("Minimum search depth for overlap checks." +
		"Only for 2D physics. Ignored for 3D physics.")]
		private int minSearchDepth = -1;
		[SerializeField] private float previewHeightOffset = 0.1f;
		[SerializeField, Range(1, 64)] private int maxTargets = 16;

		public TargetingStrategy Create() {
			return new AreaTargetingStrategy(
				this.includeCaster,
				this.previewPrefab,
				this.radius,
				this.layerMask,
				this.previewHeightOffset,
				this.maxTargets,
				this.minSearchDepth
			);
		}
	}




	[Serializable]
	public sealed class AreaTargetingStrategy : TargetingStrategy {
		// even though this this class is being created and destroyed every time the ability is used,
		// we still want to minimize GC allocations as much as possible, hence the use of non-alloc buffers
		// and careful structuring of data.
		// making struct wont work due to pack we need to inherit from TargetingStrategy, and we need
		// reference semantics for the preview controller and instance.
		private readonly bool _includeCaster = false;
		private readonly GameObject _previewPrefab;
		private readonly float _radius = 5f;
		private readonly LayerMask _layerMask = 1;
		private readonly int _minSearchDepth = -1;
		private readonly float _previewHeightOffset = 0.1f;
		private readonly int _maxTargets = 16;

		private static readonly Quaternion PERPENDICULAR_DIRECTION = Quaternion.Euler(90f, 0f, 0f);
		private GameObject _previewInstance;
		private AbilityAreaTargetingController _previewController;

		private Vector3 currentPoint;

		// Non-alloc buffers
		private Collider[] _results3d;
		private Collider2D[] _results2d;

		public AreaTargetingStrategy(bool includeCaster,
		 GameObject previewPrefab, float radius, LayerMask layerMask,
		 float previewHeightOffset, int maxTargets, int minSearchDepth) {

			this._includeCaster = includeCaster;
			this._previewPrefab = previewPrefab;
			this._radius = radius;
			this._layerMask = layerMask;
			this._previewHeightOffset = previewHeightOffset;
			this._maxTargets = maxTargets;
			this._minSearchDepth = minSearchDepth;
			// Init buffers
			this._results3d = new Collider[this._maxTargets];
			this._results2d = new Collider2D[this._maxTargets];

		}

		public override void Start(
			TargetingManager targetingManager,
			TargetContext casterContext,
			EffectContext effectContext,
			ITargetingReceiver onTargetResolved) {

			Begin(targetingManager, casterContext, effectContext, onTargetResolved);


			// Spawn preview
			if (this._previewPrefab != null && this.targetingManager != null) {
				this._previewInstance = UnityEngine.Object.Instantiate(
					this._previewPrefab, Vector3.zero, Quaternion.identity);

				this._previewController = this._previewInstance.GetComponent<AbilityAreaTargetingController>();

				if (this._previewController != null) {
					this._previewController.Initialize(this._radius);
				}
			}
		}

		public override void Update() {
			if (!this._isTargeting || this.targetingManager == null) return;
			if (!this.targetingManager.TryGetMouseGroundPoint(out this.currentPoint)) return;

			if (this._previewController != null) {
				if (this.effectContext.Dimension == AxisMode.TwoD) {
					this._previewController.UpdatePosition(
						new Vector3(this.currentPoint.x, this.currentPoint.y, -0.1f),
						Quaternion.identity
					);
				} else {
					this._previewController.UpdatePosition(
						this.currentPoint + Vector3.up * this._previewHeightOffset,
						PERPENDICULAR_DIRECTION
					);
				}
			}
		}

		public override void FinishTheStratrgy(bool clearOnTargetResolved = true) {
			base.FinishTheStratrgy(clearOnTargetResolved);

			if (this._previewInstance != null) {
				UnityEngine.Object.Destroy(this._previewInstance);
				this._previewInstance = null;
				this._previewController = null;
			}
			Array.Clear(this._results3d, 0, this._results3d.Length);
			Array.Clear(this._results2d, 0, this._results2d.Length);
		}

		protected override bool ExecuteResolution(Vector3 clickPoint) {
			var targets = GetTargetsInArea(clickPoint);
			if (targets.Length > 0) {
				ResolveGroupOfTargets(targets, clickPoint);
			}
			return true;
		}

		private TargetContext[] GetTargetsInArea(Vector3 point) {
			List<TargetContext> resolvedList = new();
			HashSet<IHitBoxComponent> uniqueHits = new();


			int count;

			if (this.effectContext.Dimension == AxisMode.TwoD) {
				this._results2d = Physics2D.OverlapCircleAll(point, this._radius, this._layerMask, this._minSearchDepth, this._maxTargets);
				count = this._results2d.Length;
				for (int i = 0; i < count; i++) {
					ProcessHit(this._results2d[i], uniqueHits, resolvedList);
				}
			} else {
				count = Physics.OverlapSphereNonAlloc(point, this._radius, this._results3d, this._layerMask);

				for (int i = 0; i < count; i++) {
					ProcessHit(this._results3d[i], uniqueHits, resolvedList);
				}
			}

			return resolvedList.ToArray();
		}

		private void ProcessHit(UnityEngine.Component col, HashSet<IHitBoxComponent> uniqueHits, List<TargetContext> results) {
			var ctx = TargetContext.Create(col);
			var casterHitBox = this.casterContext.HitBox;
			// 1. Ensure context and hitbox exist
			// 2. Ensure we haven't processed this specific hitbox yet
			if (ctx?.HitBox != null && uniqueHits.Add(ctx.HitBox)) {
				// Logical Fix: 
				// If includeCaster is true, we don't care if it's the caster or not.
				// If includeCaster is false, we MUST ensure ctx.HitBox != casterHitBox.

				if (this._includeCaster || ctx.HitBox != casterHitBox) {
					results.Add(ctx);
				}

			}
		}
	}
}