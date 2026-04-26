using System;
using Kope.Component.Combat.Interface;
using UnityEngine;

namespace Kope.Component.Ability.Targeting {

	public interface ITargetingFactory {
		TargetingStrategy Create();
	}
	public interface ITargetingReceiver {
		void OnTargetingResolved(TargetContext target, EffectContext context);
	}

	[Serializable]
	public abstract class TargetingStrategy {
		protected TargetingManager targetingManager;
		protected TargetContext casterContext;
		protected EffectContext effectContext;
		protected bool _isTargeting;
		protected ITargetingReceiver _onTargetResolved;

		public bool IsTargeting => this._isTargeting;

		public abstract void Start(
			TargetingManager targetingManager,
			TargetContext casterContext,
			EffectContext effectContext,
			ITargetingReceiver onTargetResolved);

		public virtual void Update() { }

		public virtual void FinishTheStratrgy(bool clearOnTargetResolved = true) {
			if (!this._isTargeting) return;
			this._isTargeting = false;
			if (clearOnTargetResolved) {
				this._onTargetResolved = null;
			}
			var manager = this.targetingManager;
			this.targetingManager = null;
			if (manager != null) manager.NotifyStrategyFinished(this);
		}

		/// <summary>
		/// Must be called on Start Method of the dervived class to properly initialize the
		/// strategy and set it in the manager.
		/// setStrategyInManager is provided for self targeting strategies that don't need to be
		///  registered with the manager for target resolution.
		/// </summary>
		protected void Begin(
		TargetingManager targetingManager,
		TargetContext casterContext,
		EffectContext effectContext,
		ITargetingReceiver onTargetResolved,
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
			// ExecuteResolution returns false if the callback must stay alive past this call
			// (e.g. projectile strategies defer resolution until hit or expiry).
			// FinishTheStrategy is always called here so derived classes can't leave the strategy lingering.
			bool shouldClearCallback = ExecuteResolution(clickPoint);
			FinishTheStratrgy(shouldClearCallback);
		}

		/// <summary>
		/// Performs the actual targeting logic (e.g. raycast, AOE check, projectile spawn).
		/// Must call <see cref="ResolveSingleTarget"/> or <see cref="ResolveGroupOfTargets"/> before returning.
		/// </summary>
		/// <returns>
		/// <c>true</c> if the callback can be cleared immediately after resolution (instant strategies).
		/// <c>false</c> if the callback must stay alive past this call — used by strategies that resolve
		/// asynchronously (e.g. <see cref="ProjectileTargetingStrategy"/>, which fires the callback on hit or expiry).
		/// </returns>
		/// <param name="clickPoint">World-space position provided by the TargetingManager.</param>
		protected abstract bool ExecuteResolution(Vector3 clickPoint);


		protected virtual void ResolveSingleTarget(TargetContext target, Vector3? hitPoint = null) {
			if (target.HitBox == null) return;

			EffectContext specificContext = this.effectContext;

			if (hitPoint.HasValue) {
				specificContext.HitPoint = hitPoint.Value;
			}

			this._onTargetResolved?.OnTargetingResolved(target, specificContext);
		}

		protected virtual void ResolveGroupOfTargets(TargetContext[] targets, Vector3? hitPoint = null) {
			var context = this.effectContext;
			if (hitPoint.HasValue) {
				context.HitPoint = hitPoint.Value;
			}
			foreach (var target in targets) {
				if (target.HitBox == null) continue;
				this._onTargetResolved?.OnTargetingResolved(target, context);
			}
		}
	}
}