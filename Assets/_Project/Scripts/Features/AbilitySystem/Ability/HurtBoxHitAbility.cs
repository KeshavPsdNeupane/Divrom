using System.Collections.Generic;
using Kope.Component.Combat.Interface;
using Kope.Component.HitBox.Interface;
using Kope.Component.Health.Interface;
using Kope.Component.Movement;
using UnityEngine;
using Kope.Core.Attributes;

[CreateAssetMenu(menuName = "Scriptable Object/Abilities/HurtBox Hit Ability", fileName = "HurtBoxHitAbility")]
public class HurtBoxHitAbility : AbilityBase {
	[SerializeField] private CombatType combatType = CombatType.Entity;
	[SerializeReference, SubclassSelector] private List<IEffectFactory<ICombatable>> damageEffects = new();
	[SerializeReference, SubclassSelector] private List<IEffectFactory<IHealable>> healEffects = new();
	[SerializeReference, SubclassSelector] private List<IEffectFactory<IStunnable>> stunEffects = new();

	public override void Execute(TargetContext target, EffectContext casterEffectContext) {
		if (target.DamageTarger == null || target.DamageTarger.HurtBox == null) return;
		if (casterEffectContext.Caster == null) return;
		var type = combatType;
		// target.Target.HurtBox.HitEntity(casterEffectContext.Caster, this.combatType, casterEffectContext, this.damageEffects, this.stunEffects);

		// if (target.HealableTarget != null && this.healEffects.Count > 0) {
		// 	for (int i = 0; i < this.healEffects.Count; i++) {
		// 		var effect = this.healEffects[i]?.Create(casterEffectContext);
		// 		effect?.Apply(target.HealableTarget);
		// 	}
		// }

	}

	protected override void HandleCastVFX(TargetContext target) {
	}

	protected override void HandleRunningVFX(TargetContext target) {
	}

	protected override void HandleSFX(TargetContext target) {
	}
}