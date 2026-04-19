// TargetingStrategy.cs
using System;
using Kope.Component.Combat.Interface;
using UnityEngine;

namespace Kope.Component.Ability.Targeting {

	public interface ITargetingFactory {
		TargetingStrategy Create();
	}

	[Serializable]
	public abstract class TargetingStrategy {
		protected TargetingManager targetingManager;
		protected TargetContext casterContext;
		protected EffectContext effectContext;
		protected bool isTargeting;
		private Action<TargetContext, EffectContext> _onTargetResolved;

		public bool IsTargeting => this.isTargeting;

		public abstract void Start(
			TargetingManager targetingManager,
			in TargetContext casterContext,
			EffectContext effectContext,
			Action<TargetContext, EffectContext> onTargetResolved);

		public virtual void Update() { }

		public virtual void Cancel() {
			this.isTargeting = false;
			this._onTargetResolved = null;
			if (this.targetingManager != null) {
				this.targetingManager.ClearCurrentStrategy(this);
			}
		}

		protected void Begin(
			TargetingManager targetingManager,
			in TargetContext casterContext,
			EffectContext effectContext,
			Action<TargetContext, EffectContext> onTargetResolved) {
			this.targetingManager = targetingManager;
			this.casterContext = casterContext;
			this.effectContext = effectContext;
			this._onTargetResolved = onTargetResolved;
			this.isTargeting = true;
			if (this.targetingManager != null) {
				this.targetingManager.SetCurrentStrategy(this);
			}
		}

		protected void ResolveTarget(in TargetContext target, Vector3? hitPoint = null) {
			if (target.HitBox == null) return;

			var context = this.effectContext;
			if (hitPoint.HasValue) {
				context.HitPoint = hitPoint.Value;
			}
			this._onTargetResolved?.Invoke(target, context);
		}
	}
}