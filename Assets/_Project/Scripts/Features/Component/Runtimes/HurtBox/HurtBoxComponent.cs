using System;
using System.Collections.Generic;
using Kope.Component.Combat.Interface;
using Kope.Component.Health.Interface;
using Kope.Component.HitBox.Interface;
using Kope.Component.Movement;
using Kope.Core.Init;
using UnityEngine;

namespace Kope.Component.HitBox {
	public readonly struct CombatibleHitInfo {
		public readonly GameObject Caster;
		public readonly CombatType CombatType;
		public readonly EffectContext EffectContext;
		public readonly IReadOnlyList<IEffectFactory<ICombatable>> Effects;

		public CombatibleHitInfo(
			GameObject caster,
			CombatType combatType = CombatType.Entity,
			EffectContext effectContext = default,
			IReadOnlyList<IEffectFactory<ICombatable>> effects = null) {

			this.Caster = caster;
			this.CombatType = combatType;
			this.EffectContext = effectContext;
			this.Effects = effects;
		}
	}
	public readonly struct HealableHitInfo {
		public readonly GameObject Caster;
		public readonly CombatType CombatType;
		public readonly EffectContext EffectContext;
		public readonly IReadOnlyList<IEffectFactory<IHealable>> Effects;
		public HealableHitInfo(
			GameObject caster,
			CombatType combatType = CombatType.Entity,
			EffectContext effectContext = default,
			IReadOnlyList<IEffectFactory<IHealable>> effects = null) {

			this.Caster = caster;
			this.CombatType = combatType;
			this.EffectContext = effectContext;
			this.Effects = effects;
		}
	}
	public readonly struct StunnableHitInfo {
		public readonly GameObject Caster;
		public readonly CombatType CombatType;
		public readonly EffectContext EffectContext;
		public readonly IReadOnlyList<IEffectFactory<IStunnable>> StunEffects;
		public StunnableHitInfo(
			GameObject caster,
			CombatType combatType = CombatType.Entity,
			EffectContext effectContext = default,
			IReadOnlyList<IEffectFactory<IStunnable>> stunEffects = null) {
			this.Caster = caster;
			this.CombatType = combatType;
			this.EffectContext = effectContext;
			this.StunEffects = stunEffects;
		}
		public readonly struct KnockableHitInfo {
			public readonly GameObject Caster;
			public readonly CombatType CombatType;
			public readonly EffectContext EffectContext;
			public readonly IReadOnlyList<IEffectFactory<IKnockbackable>> KnockEffects;
			public KnockableHitInfo(
				GameObject caster,
				CombatType combatType = CombatType.Entity,
				EffectContext effectContext = default,
				IReadOnlyList<IEffectFactory<IKnockbackable>> knockEffects = null) {
				this.Caster = caster;
				this.CombatType = combatType;
				this.EffectContext = effectContext;
				this.KnockEffects = knockEffects;
			}
		}


		public class HitBoxComponent : InitializableBase, IHurtBoxComponent {
			[SerializeField] private CombatType combatType = CombatType.Entity;
			[SerializeField] private Collider hurtBoxCollider;
			[SerializeField] private bool isInvulnerable;

			public event Action<CombatibleHitInfo> OnHitCombatible;
			public event Action<HealableHitInfo> OnHitHealable;
			public event Action<StunnableHitInfo> OnHitStunnable;
			public event Action<KnockableHitInfo> OnHitKnockable;
			public Collider HurtBoxCollider => hurtBoxCollider;
			public CombatType CombatType => combatType;


			protected override bool OnInit() {
				if (this.hurtBoxCollider == null) {
					Debug.LogError($"HurtBoxComponent on {gameObject.name} has no Collider assigned." +
					GetParentGameObjectHeirarchyMessage());
					return false;
				}
				return true;
			}

			public void HitCombatible(
				GameObject caster,
				CombatType combatType = CombatType.Entity,
				in EffectContext effectContext = default,
				IReadOnlyList<IEffectFactory<ICombatable>> effects = null) {
				if (caster == null) return;
				this.OnHitCombatible?.Invoke(new CombatibleHitInfo(caster, combatType, effectContext, effects));
			}

			public void HitHealable(
				GameObject caster,
				CombatType combatType = CombatType.Entity,
				in EffectContext effectContext = default,
				IReadOnlyList<IEffectFactory<IHealable>> effects = null) {
				if (caster == null) return;
				this.OnHitHealable?.Invoke(new HealableHitInfo(caster, combatType, effectContext, effects));
			}
			public void HitStunnable(
				GameObject caster,
				CombatType combatType = CombatType.Entity,
				in EffectContext effectContext = default,
				IReadOnlyList<IEffectFactory<IStunnable>> stunEffects = null) {
				if (caster == null) return;
				this.OnHitStunnable?.Invoke(new StunnableHitInfo(caster, combatType, effectContext, stunEffects));
			}
			public void HitKnockable(
				GameObject caster,
				CombatType combatType = CombatType.Entity,
				in EffectContext effectContext = default,
				IReadOnlyList<IEffectFactory<IKnockbackable>> knockEffects = null) {
				if (caster == null) return;
				this.OnHitKnockable?.Invoke(new KnockableHitInfo(caster, combatType, effectContext, knockEffects));
			}
		}
	}

}


