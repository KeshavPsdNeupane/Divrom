using System.Collections.Generic;
using Kope.Component.Combat.Interface;
using Kope.Component.HitBox.Interface;
using Kope.Core.Attributes;
using UnityEngine;

[CreateAssetMenu(menuName = "Scriptable Objects/Abilities/Damage Ability", fileName = "DamageAbility")]
public class DamageAbility : AbilityBase {
	[SerializeField] private HitTargetType applicableHitTargetType = HitTargetType.Entity;
	[SerializeReference, SubclassSelector] private List<IEffectFactory<ICombatable>> damageEffects = new();

	public override void Execute(TargetContext target, EffectContext context) {
		if (target.HitBox == null || context.Caster == null) return;
		if (target.HitBox.CombatType != this.applicableHitTargetType) return;

		var position = GetTargetPosition(target);
		// running mean vfx that will be applied when hit to certain area or target, position is
		// where the vfx will be spawned, can be used by abilities that want to spawn a vfx on the 
		// ground at the target location, or a vfx that follows the target around while the ability is active
		SpawnRunningVfx(position);
		var hitbox = target.HitBox;
		hitbox.HitCombatible(context, target.HitBox.CombatType, this.damageEffects);
	}
}
