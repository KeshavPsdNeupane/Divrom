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

		if (target.HitBox == null || casterEffectContext.Caster == null) return;
		var type = combatType;
		if (target.HitBox != null && target.HitBox.CombatType == CombatType.Entity) {
			var hitbox = target.HitBox;
			hitbox.HitCombatible(casterEffectContext.Caster, type, casterEffectContext, damageEffects);
			hitbox.HitHealable(casterEffectContext.Caster, type, casterEffectContext, healEffects);
			hitbox.HitStunnable(casterEffectContext.Caster, type, casterEffectContext, stunEffects);
			// if the ability has any knockback effects, we would also call hitbox.HitKnockable here,
			// but this ability doesn't have any knockback effects so we don't need to.
			// below is the example for the routing.
			//hitbox.HitKnockable(casterEffectContext.Caster, type, casterEffectContext, knockEffects);
		}

	}

	protected override void HandleCastVFX(TargetContext target) {
	}

	protected override void HandleRunningVFX(TargetContext target) {
	}

	protected override void HandleSFX(TargetContext target) {
	}
}