// TargetingStrategy.cs
using System;
using System.Collections.Generic;
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
		protected bool _isTargeting;
		private Action<TargetContext, EffectContext> _onTargetResolved;

		public bool IsTargeting => this._isTargeting;

		public abstract void Start(
			TargetingManager targetingManager,
			TargetContext casterContext,
			EffectContext effectContext,
			Action<TargetContext, EffectContext> onTargetResolved);

		public virtual void Update() { }

		public virtual void FinishTheStratrgy() {
			if (!this._isTargeting) return;
			this._isTargeting = false;
			this._onTargetResolved = null;
			var manager = this.targetingManager;
			this.targetingManager = null;
			if (manager != null) manager.NotifyStrategyFinished(this);
		}

		/// <summary>
		/// Must be called on Start Method of the dervived class to properly initialize the
		/// strategy and set it in the manager.
		/// </summary>
		/// <param name="targetingManager"></param>
		/// <param name="casterContext"></param>
		/// <param name="effectContext"></param>
		/// <param name="onTargetResolved"></param>
		/// <param name="setStraegyInManager"></param>
		protected void Begin(
			TargetingManager targetingManager,
			TargetContext casterContext,
			EffectContext effectContext,
			Action<TargetContext, EffectContext> onTargetResolved,
			// this is so that self targeting strategies can choose to not
			// set themselves in the manager, since they won't be using the manager 
			// for target resolution and thus don't need to be registered with it.
			bool setStraegyInManager = true
			) {

			this.targetingManager = targetingManager;
			this.casterContext = casterContext;
			this.effectContext = effectContext;
			this._onTargetResolved = onTargetResolved;
			this._isTargeting = true;
			if (setStraegyInManager && this.targetingManager != null) {
				this.targetingManager.SetCurrentStrategy(this);
			}
		}


		public void ProcessInput(Vector3 clickPoint) {
			if (!this._isTargeting) return;
			ExecuteResolution(clickPoint);
			FinishTheStratrgy();
		}

		/// <summary>
		/// Implemented by derived classes to perform the actual targeting logic (e.g., Raycasting or AOE checks).
		/// This method must call Resolve() or ResolveGroup() to report results before the strategy automatically closes.
		/// </summary>
		/// <param name="clickPoint">The world-space position provided by the TargetingManager.</param>
		protected abstract void ExecuteResolution(Vector3 clickPoint);


		protected virtual void ResolveSingleTarget(TargetContext target, Vector3? hitPoint = null) {
			if (target.HitBox == null) return;

			EffectContext specificContext = this.effectContext;

			if (hitPoint.HasValue) {
				specificContext.HitPoint = hitPoint.Value;
			}

			this._onTargetResolved?.Invoke(target, specificContext);
		}

		protected virtual void ResolveGroupOfTargets(TargetContext[] targets, Vector3? hitPoint = null) {
			var context = this.effectContext;
			if (hitPoint.HasValue) {
				context.HitPoint = hitPoint.Value;
			}
			foreach (var target in targets) {
				if (target.HitBox == null) continue;
				this._onTargetResolved?.Invoke(target, context);
			}
		}
	}
}