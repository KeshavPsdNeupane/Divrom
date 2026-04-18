using System;
using System.Collections.Generic;
using Kope.Component.Combat.Interface;
using Kope.Component.Health.Interface;
using Kope.Component.Movement;
using UnityEngine;
using static Kope.Component.HitBox.StunnableHitInfo;

namespace Kope.Component.HitBox.Interface {
	public enum HitTargetType {
		Entity,
		Destructible,
		Projectile,
		Other
	}

	public readonly struct HurtBoxHitInfo {
		public readonly HitBoxComponent HurtBox;
		public readonly GameObject Caster;
		public readonly HitTargetType CombatType;
		public readonly EffectContext EffectContext;
		public readonly IReadOnlyList<IEffectFactory<ICombatable>> Effects;
		public readonly IReadOnlyList<IEffectFactory<IStunnable>> StunEffects;

		public GameObject SourceGameObject => this.Caster != null ? this.Caster : null;

		public HurtBoxHitInfo(
			HitBoxComponent hurtBox,
			GameObject caster,
			HitTargetType combatType = HitTargetType.Entity,
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
		public HitTargetType CombatType { get; }
		public event Action<CombatibleHitInfo> OnHitCombatible;
		public event Action<HealableHitInfo> OnHitHealable;
		public event Action<StunnableHitInfo> OnHitStunnable;
		public event Action<KnockableHitInfo> OnHitKnockable;
		public void HitCombatible(
			in EffectContext effectContext = default,
			HitTargetType combatType = HitTargetType.Entity,
			IReadOnlyList<IEffectFactory<ICombatable>> effects = null);
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
	}

}