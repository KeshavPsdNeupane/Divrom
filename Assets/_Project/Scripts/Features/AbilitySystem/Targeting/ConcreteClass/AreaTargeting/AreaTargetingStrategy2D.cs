using System;
using System.Collections.Generic;
using Kope.AbilitySystem;
using Kope.Component.HitBox.Interface;
using UnityEngine;
namespace Kope.Component.Ability.Targeting {

	[Serializable]
	public sealed class AreaTargetingStrategy2D : AreaTargetingStrategy {
		private readonly int _minSearchDepth;
		private Collider2D[] _results;

		public AreaTargetingStrategy2D(bool includeCaster, GameObject previewPrefab,
			float radius, LayerMask layerMask, float previewHeightOffset,
			int maxTargets, int minSearchDepth, Color previewColor)
			: base(includeCaster, previewPrefab, radius, layerMask, previewHeightOffset, maxTargets, previewColor) {
			this._minSearchDepth = minSearchDepth;
			this._results = new Collider2D[maxTargets];
		}

		protected override void UpdatePreviewPosition(AbilityAreaTargetingController controller) {
			controller.UpdatePosition(
				new Vector3(this.currentPoint.x, this.currentPoint.y, -0.1f),
				Quaternion.identity);
		}

		protected override TargetContext[] GetTargetsInArea(Vector3 point) {
			var resolvedList = new List<TargetContext>();
			var uniqueHits = new HashSet<IHitBoxComponent>();
			this._results = Physics2D.OverlapCircleAll(point, this._radius, this._layerMask,
				this._minSearchDepth, this._maxTargets);
			foreach (var col in this._results)
				ProcessHit(col, uniqueHits, resolvedList);
			return resolvedList.ToArray();
		}

		protected override void ClearBuffers() =>
			Array.Clear(this._results, 0, this._results.Length);
	}
}