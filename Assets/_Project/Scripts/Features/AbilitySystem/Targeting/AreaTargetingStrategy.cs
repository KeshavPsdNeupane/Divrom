using System;
using System.Collections.Generic;
using Kope.Component.Combat.Interface;
using UnityEngine;
using Kope.Component.HitBox.Interface;
using Kope.Core;

namespace Kope.Component.Ability.Targeting {

	[Serializable]
	public sealed class AreaTargetingStrategy : TargetingStrategy, ITargetingFactory {
		[SerializeField] private bool includeCaster = false;
		[SerializeField] private GameObject previewPrefab;
		[SerializeField, Min(0.001f)] private float radius = 5f;
		[SerializeField] LayerMask layerMask = 1;
		[SerializeField] private float previewHeightOffset = 0.1f;
		[SerializeField, Range(1, 64)] private int maxTargets = 16;

		private static readonly Quaternion PERPENDICULAR_DIRECTION = Quaternion.Euler(90f, 0f, 0f);

		private GameObject previewInstance;
		private AbilityAreaTargetingController previewController;

		private Vector3 currentPoint;

		// Non-alloc buffers
		private Collider[] _results3d;
		private Collider2D[] _results2d;

		public TargetingStrategy Create() {
			return new AreaTargetingStrategy {
				includeCaster = this.includeCaster,
				previewPrefab = this.previewPrefab,
				radius = this.radius,
				layerMask = this.layerMask,
				previewHeightOffset = this.previewHeightOffset,
				maxTargets = this.maxTargets
			};
		}

		public override void Start(
			TargetingManager targetingManager,
			TargetContext casterContext,
			EffectContext effectContext,
			Action<TargetContext, EffectContext> onTargetResolved) {

			Begin(targetingManager, casterContext, effectContext, onTargetResolved);

			// Init buffers
			this._results3d = new Collider[maxTargets];
			this._results2d = new Collider2D[maxTargets];

			// Spawn preview
			if (this.previewPrefab != null && this.targetingManager != null) {
				this.previewInstance = UnityEngine.Object.Instantiate(
					this.previewPrefab, Vector3.zero, Quaternion.identity);

				this.previewController = this.previewInstance.GetComponent<AbilityAreaTargetingController>();

				if (this.previewController != null) {
					this.previewController.Initialize(this.radius);
				}
			}
		}

		public override void Update() {
			if (!this._isTargeting || this.targetingManager == null) return;
			if (!this.targetingManager.TryGetMouseGroundPoint(out this.currentPoint)) return;

			if (this.previewController != null) {
				if (this.effectContext.Dimension == AxisMode.TwoD) {
					this.previewController.UpdatePosition(
						new Vector3(this.currentPoint.x, this.currentPoint.y, -0.1f),
						Quaternion.identity
					);
				} else {
					this.previewController.UpdatePosition(
						this.currentPoint + Vector3.up * this.previewHeightOffset,
						PERPENDICULAR_DIRECTION
					);
				}
			}
		}

		public override void FinishTheStratrgy(bool clearOnTargetResolved = true) {
			base.FinishTheStratrgy(clearOnTargetResolved);

			if (this.previewInstance != null) {
				UnityEngine.Object.Destroy(this.previewInstance);
				this.previewInstance = null;
				this.previewController = null;
			}

			this._results3d = null;
			this._results2d = null;
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
				this._results2d = Physics2D.OverlapCircleAll(point, this.radius, this.layerMask);
				count = this._results2d.Length;

				for (int i = 0; i < count; i++) {
					ProcessHit(this._results2d[i], uniqueHits, resolvedList);
				}
			} else {
				count = Physics.OverlapSphereNonAlloc(point, this.radius, this._results3d, this.layerMask);

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

				if (this.includeCaster || ctx.HitBox != casterHitBox) {
					results.Add(ctx);
				}

			}
		}
	}
}