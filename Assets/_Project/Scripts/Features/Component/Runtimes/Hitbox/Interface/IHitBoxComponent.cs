using System;
using System.Collections.Generic;
using Kope.Character.Stats;
using Kope.Component.Combat.Interface;
using Kope.Component.Health.Interface;
using Kope.Component.Movement;
using UnityEngine;
namespace Kope.Component.HitBox.Interface {
	public enum HitTargetType {
		Entity,
		Destructible,
		Projectile,
		Other
	}

	public readonly struct DamagableHitInfo {
		public readonly HitTargetType CombatType;
		public readonly EffectContext EffectContext;
		public readonly IReadOnlyList<IEffectFactory<IDamagable>> Effects;

		public DamagableHitInfo(
			EffectContext effectContext = default,
			HitTargetType combatType = HitTargetType.Entity,
			IReadOnlyList<IEffectFactory<IDamagable>> effects = null) {
			this.CombatType = combatType;
			this.EffectContext = effectContext;
			this.Effects = effects;
		}
	}
	public readonly struct HealableHitInfo {
		public readonly HitTargetType CombatType;
		public readonly EffectContext EffectContext;
		public readonly IReadOnlyList<IEffectFactory<IHealable>> Effects;
		public HealableHitInfo(
			in EffectContext effectContext = default,
			HitTargetType combatType = HitTargetType.Entity,
			IReadOnlyList<IEffectFactory<IHealable>> effects = null) {
			this.CombatType = combatType;
			this.EffectContext = effectContext;
			this.Effects = effects;
		}
	}

	public readonly struct StunnableHitInfo {
		public readonly HitTargetType CombatType;
		public readonly EffectContext EffectContext;
		public readonly IReadOnlyList<IEffectFactory<IStunnable>> StunEffects;
		public StunnableHitInfo(
			in EffectContext effectContext = default,
			HitTargetType combatType = HitTargetType.Entity,
			IReadOnlyList<IEffectFactory<IStunnable>> stunEffects = null) {
			this.CombatType = combatType;
			this.EffectContext = effectContext;
			this.StunEffects = stunEffects;
		}
	}
	public readonly struct KnockableHitInfo {
		public readonly HitTargetType CombatType;
		public readonly EffectContext EffectContext;
		public readonly IReadOnlyList<IEffectFactory<IKnockbackable>> KnockEffects;
		public KnockableHitInfo(
			in EffectContext effectContext = default,
			HitTargetType combatType = HitTargetType.Entity,
			IReadOnlyList<IEffectFactory<IKnockbackable>> knockEffects = null) {
			this.CombatType = combatType;
			this.EffectContext = effectContext;
			this.KnockEffects = knockEffects;
		}
	}
	public readonly struct StatChangeHitInfo {
		public readonly HitTargetType CombatType;
		public readonly EffectContext EffectContext;
		public readonly IReadOnlyList<IEffectFactory<IStatSystem>> StatEffects;
		public StatChangeHitInfo(
			in EffectContext effectContext = default,
			HitTargetType combatType = HitTargetType.Entity,
			IReadOnlyList<IEffectFactory<IStatSystem>> statEffects = null) {
			this.CombatType = combatType;
			this.EffectContext = effectContext;
			this.StatEffects = statEffects;
		}
	}

	public interface IHitBoxComponent {
		Transform Transform { get; }
		public HitTargetType CombatType { get; }
		public event Action<DamagableHitInfo> OnHitCombatible;
		public event Action<HealableHitInfo> OnHitHealable;
		public event Action<StunnableHitInfo> OnHitStunnable;
		public event Action<KnockableHitInfo> OnHitKnockable;
		public event Action<StatChangeHitInfo> OnHitStatChange;
		public void HitCombatible(
			in EffectContext effectContext = default,
			HitTargetType combatType = HitTargetType.Entity,
			IReadOnlyList<IEffectFactory<IDamagable>> effects = null);
		public void HitHealable(
				in EffectContext effectContext = default,
				HitTargetType combatType = HitTargetType.Entity,
				IReadOnlyList<IEffectFactory<IHealable>> effects = null);
		public void HitStunnable(
			in EffectContext effectContext = default,
			HitTargetType combatType = HitTargetType.Entity,
			IReadOnlyList<IEffectFactory<IStunnable>> stunEffects = null);
		public void HitKnockable(
		in EffectContext effectContext = default,
		HitTargetType combatType = HitTargetType.Entity,
		IReadOnlyList<IEffectFactory<IKnockbackable>> knockEffects = null);
		public void HitStatChange(
			in EffectContext effectContext = default,
			HitTargetType combatType = HitTargetType.Entity,
			IReadOnlyList<IEffectFactory<IStatSystem>> statEffects = null);
	}

}
