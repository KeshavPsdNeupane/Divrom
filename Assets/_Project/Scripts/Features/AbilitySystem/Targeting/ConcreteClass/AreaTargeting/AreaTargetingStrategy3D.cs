using System;
using System.Collections.Generic;
using Kope.AbilitySystem;
using Kope.Component.HitBox.Interface;
using UnityEngine;

namespace Kope.Component.Ability.Targeting {
	[Serializable]
	public sealed class AreaTargetingStrategy3D : AreaTargetingStrategy {
		private readonly Collider[] _results;
		private static readonly Quaternion PERPENDICULAR_DIRECTION = Quaternion.Euler(90f, 0f, 0f);

		public AreaTargetingStrategy3D(bool includeCaster, GameObject previewPrefab,
			float radius, float maxRayDistance, LayerMask layerMask, float previewHeightOffset, int maxTargets, Color previewColor)
			: base(includeCaster, previewPrefab, radius, maxRayDistance, layerMask, previewHeightOffset, maxTargets, previewColor) {
			this._results = new Collider[maxTargets];
			this._results = new Collider[maxTargets];
		}

		protected override void UpdatePreviewPosition(AbilityAreaTargetingController controller) {
			controller.UpdatePosition(
				this.currentAOECirclePoint + Vector3.up * this._previewHeightOffset,
				PERPENDICULAR_DIRECTION);
		}

		protected override TargetContext[] GetTargetsInArea(Vector3 point) {
			var resolvedList = new List<TargetContext>();
			var uniqueHits = new HashSet<IHitBoxComponent>();
			int count = Physics.OverlapSphereNonAlloc(point, this._radius, this._results, this._layerMask);
			for (int i = 0; i < count; i++)
				ProcessHit(this._results[i], uniqueHits, resolvedList);
			return resolvedList.ToArray();
		}
		protected override void ClearBuffers() =>
			Array.Clear(this._results, 0, this._results.Length);
	}
}