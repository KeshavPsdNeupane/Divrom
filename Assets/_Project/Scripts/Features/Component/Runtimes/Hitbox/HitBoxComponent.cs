using System;
using System.Collections.Generic;
using Kope.Component.Combat.Interface;
using Kope.Component.Health.Interface;
using Kope.Component.HitBox.Interface;
using Kope.Component.Movement;
using Kope.Core.Init;
using UnityEngine;

namespace Kope.Component.HitBox {
	public class HitBoxComponent : InitializableBase, IHitBoxComponent {
		[SerializeField] private HitTargetType combatType = HitTargetType.Entity;
		[SerializeField] private Collider2D hurtBoxCollider;
		[SerializeField] private bool isInvulnerable;

		public event Action<CombatibleHitInfo> OnHitCombatible;
		public event Action<HealableHitInfo> OnHitHealable;
		public event Action<StunnableHitInfo> OnHitStunnable;
		public event Action<KnockableHitInfo> OnHitKnockable;
		public Collider2D HurtBoxCollider => hurtBoxCollider;
		public HitTargetType CombatType => combatType;


		protected override bool OnInit() {
			if (this.hurtBoxCollider == null) {
				Debug.LogError($"HurtBoxComponent on {gameObject.name} has no Collider assigned." +
				GetParentGameObjectHeirarchyMessage());
				return false;
			}
			this.hurtBoxCollider.isTrigger = true;
			return true;
		}

		public void HitCombatible(
			in EffectContext effectContext = default,
			HitTargetType combatType = HitTargetType.Entity,
			IReadOnlyList<IEffectFactory<ICombatable>> effects = null) {
			// using default since the effectContext is a struct, so it won't be null, 
			// but we can check if the Caster is null to determine if it's a valid context.
			// and also using "in" to avoid copying the struct since it might be large.
			if (effectContext == default || effectContext.Caster == null || effects == null) return;
			this.OnHitCombatible?.Invoke(new CombatibleHitInfo(effectContext, combatType, effects));
		}

		public void HitHealable(
			in EffectContext effectContext = default,
			HitTargetType combatType = HitTargetType.Entity,
			IReadOnlyList<IEffectFactory<IHealable>> effects = null) {
			if (effectContext == default || effectContext.Caster == null || effects == null) return;
			this.OnHitHealable?.Invoke(new HealableHitInfo(effectContext, combatType, effects));
		}
		public void HitStunnable(
			in EffectContext effectContext = default,
			HitTargetType combatType = HitTargetType.Entity,
			IReadOnlyList<IEffectFactory<IStunnable>> stunEffects = null) {
			if (effectContext == default || effectContext.Caster == null || stunEffects == null) return;
			this.OnHitStunnable?.Invoke(new StunnableHitInfo(effectContext, combatType, stunEffects));
		}
		public void HitKnockable(
			in EffectContext effectContext = default,
			HitTargetType combatType = HitTargetType.Entity,
			IReadOnlyList<IEffectFactory<IKnockbackable>> knockEffects = null) {
			if (effectContext == default || effectContext.Caster == null || knockEffects == null) return;
			this.OnHitKnockable?.Invoke(new KnockableHitInfo(effectContext, combatType, knockEffects));
		}
	}
}

