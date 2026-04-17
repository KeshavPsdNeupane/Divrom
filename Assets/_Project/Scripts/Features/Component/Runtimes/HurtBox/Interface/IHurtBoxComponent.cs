using System;
using System.Collections.Generic;
using Kope.Component.Combat.Interface;
using Kope.Component.Health.Interface;
using Kope.Component.Movement;
using UnityEngine;
using static Kope.Component.HitBox.StunnableHitInfo;

namespace Kope.Component.HitBox.Interface {
	public enum CombatType {
		Entity,
		Destructible,
		Projectile,
		Other
	}

	public readonly struct HurtBoxHitInfo {
		public readonly HitBoxComponent HurtBox;
		public readonly GameObject Caster;
		public readonly CombatType CombatType;
		public readonly EffectContext EffectContext;
		public readonly IReadOnlyList<IEffectFactory<ICombatable>> Effects;
		public readonly IReadOnlyList<IEffectFactory<IStunnable>> StunEffects;

		public GameObject SourceGameObject => this.Caster != null ? this.Caster : null;

		public HurtBoxHitInfo(
			HitBoxComponent hurtBox,
			GameObject caster,
			CombatType combatType = CombatType.Entity,
			EffectContext effectContext = default,
			IReadOnlyList<IEffectFactory<ICombatable>> effects = null,
			IReadOnlyList<IEffectFactory<IStunnable>> stunEffects = null) {


			this.HurtBox = hurtBox;
			this.Caster = caster;
			this.CombatType = combatType;
			this.EffectContext = effectContext;
			this.Effects = effects;
			this.StunEffects = stunEffects;
		}
	}

	public interface IHurtBoxComponent {
		public CombatType CombatType { get; }
		public event Action<CombatibleHitInfo> OnHitCombatible;
		public event Action<HealableHitInfo> OnHitHealable;
		public event Action<StunnableHitInfo> OnHitStunnable;
		public void HitCombatible(
			GameObject caster,
			CombatType combatType = CombatType.Entity,
			in EffectContext effectContext = default,
			IReadOnlyList<IEffectFactory<ICombatable>> effects = null);
		public void HitHealable(
				GameObject caster,
				CombatType combatType = CombatType.Entity,
				in EffectContext effectContext = default,
				IReadOnlyList<IEffectFactory<IHealable>> effects = null);
		public void HitStunnable(
			GameObject caster,
			CombatType combatType = CombatType.Entity,
			in EffectContext effectContext = default,
			IReadOnlyList<IEffectFactory<IStunnable>> stunEffects = null);
		public void HitKnockable(
		GameObject caster,
		CombatType combatType = CombatType.Entity,
		in EffectContext effectContext = default,
		IReadOnlyList<IEffectFactory<IKnockbackable>> knockEffects = null);
	}

}