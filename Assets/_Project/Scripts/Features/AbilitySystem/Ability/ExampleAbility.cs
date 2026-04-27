// ExampleAbility.cs
using System.Collections.Generic;
using Kope.Component.Combat.Interface;
using Kope.Component.Health.Interface;
using Kope.Component.HitBox.Interface;
using Kope.Component.Movement;
using Kope.Core.Attributes;
using UnityEngine;

[CreateAssetMenu(menuName = "Scriptable Objects/Abilities/Example Ability", fileName = "ExampleAbility")]
public class ExampleAbility : AbilityBase
{
	[SerializeField, Tooltip("The type of hit targets this ability can affect.")]
	private HitTargetType applicableHitTargetType = HitTargetType.Entity;
	[SerializeReference, SubclassSelector]
	private List<IEffectFactory<IDamagable>> damageEffects = new();
	[SerializeReference, SubclassSelector]
	private List<IEffectFactory<IHealable>> healEffects = new();
	[SerializeReference, SubclassSelector]
	private List<IEffectFactory<IStunnable>> stunEffects = new();
	[SerializeReference, SubclassSelector]
	private List<IEffectFactory<IKnockbackable>> knockEffects = new();

	public override void Execute(TargetContext target, EffectContext context)
	{
		if (target.HitBox == null || context.Caster == null) return;
		if (target.HitBox.CombatType != this.applicableHitTargetType) return;

		var position = GetTargetPosition(target);
		// running mean vfx that will be applied when hit to certain area or target, position is
		// where the vfx will be spawned, can be used by abilities that want to spawn a vfx on the 
		// ground at the target location, or a vfx that follows the target around while the ability is active
		SpawnRunningVfx(position);

		var hitbox = target.HitBox;
		hitbox.HitCombatible(context, target.HitBox.CombatType, this.damageEffects);
		hitbox.HitHealable(context, target.HitBox.CombatType, this.healEffects);
		hitbox.HitStunnable(context, target.HitBox.CombatType, this.stunEffects);
		hitbox.HitKnockable(context, target.HitBox.CombatType, this.knockEffects);
	}
}