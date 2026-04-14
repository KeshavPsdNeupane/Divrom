using System;
using System.Collections.Generic;
using Kope.Component.Combat.Interface;
using UnityEngine;

namespace Kope.Component.HurtBox.Interface {
	public enum CombatType {
		Entity,
		Destructible,
		Projectile,
		Other
	}

	public readonly struct HurtBoxHitInfo {
		public readonly HurtBoxComponent HurtBox;
		public readonly GameObject Caster;
		public readonly CombatType CombatType;
		public readonly EffectContext EffectContext;
		public readonly IReadOnlyList<IEffectFactory<ICombatable>> Effects;

		public GameObject SourceGameObject => this.Caster != null ? this.Caster : null;

		public HurtBoxHitInfo(
			HurtBoxComponent hurtBox,
			GameObject caster,
			CombatType combatType = CombatType.Entity,
			EffectContext effectContext = default,
			IReadOnlyList<IEffectFactory<ICombatable>> effects = null) {


			this.HurtBox = hurtBox;
			this.Caster = caster;
			this.CombatType = combatType;
			this.EffectContext = effectContext;
			this.Effects = effects;
		}
	}

	public interface IHurtBoxComponent {
		public event Action<HurtBoxHitInfo> OnHitEntity;
		public void HitEntity(
			GameObject caster,
			CombatType combatType = CombatType.Entity,
			EffectContext effectContext = default,
			IReadOnlyList<IEffectFactory<ICombatable>> effects = null);
	}

}