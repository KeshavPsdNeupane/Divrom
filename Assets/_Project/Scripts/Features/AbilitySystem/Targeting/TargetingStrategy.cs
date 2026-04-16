using Kope.Component.Combat.Interface;
using UnityEngine;
using UnityComponent = UnityEngine.Component;

namespace Kope.Component.Ability.Targeting {
	public interface ITargetingFactory {
		TargetingStrategy Create();
	}

	[System.Serializable]
	public abstract class TargetingStrategy {
		protected AbilityBase ability;
		protected TargetingManager targetingManager;
		// since caster context can also be used as target context for self-targeting strategies, 
		// we store it as a TargetContext instead of just an ICombatComponent
		protected TargetContext casterContext;
		protected EffectContext effectContext;
		protected bool isTargeting;

		public bool IsTargeting => this.isTargeting;

		public abstract void Start(AbilityBase ability, TargetingManager targetingManager, in TargetContext casterContext, EffectContext effectContext);
		public virtual void Update() { }
		public virtual void Cancel() {
			this.isTargeting = false;
			if (this.targetingManager != null) {
				this.targetingManager.ClearCurrentStrategy(this);
			}
		}

		protected void Begin(AbilityBase ability, TargetingManager targetingManager, in TargetContext casterContext, EffectContext effectContext) {
			this.ability = ability;
			this.targetingManager = targetingManager;
			this.casterContext = casterContext;
			this.effectContext = effectContext;
			this.isTargeting = true;
			if (this.targetingManager != null) {
				this.targetingManager.SetCurrentStrategy(this);
			}
		}

		protected void ExecuteOnTarget(in TargetContext target, Vector3? HitPoint = null) {
			if (this.ability == null || target.DamageTarger == null) return;

			var context = this.effectContext;
			if (HitPoint.HasValue) {
				context.HitPoint = HitPoint.Value;
			}

			var targetPosition = target.DamageTarger is UnityComponent targetComponent
				? targetComponent.transform.position
				: this.targetingManager != null ? this.targetingManager.transform.position : Vector3.zero;

			if (this.ability.CastSfx != null) {
				AudioSource.PlayClipAtPoint(this.ability.CastSfx, targetPosition);
			}

			if (this.ability.CastVfx != null) {
				Object.Instantiate(this.ability.CastVfx, targetPosition, Quaternion.identity);
			}

			this.ability.Execute(target, context);
		}
	}
}