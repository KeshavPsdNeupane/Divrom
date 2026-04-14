using Kope.Component.Combat.Interface;
using Kope.Component.HurtBox;
using Kope.Component.HurtBox.Interface;
using UnityEngine;

[CreateAssetMenu(menuName = "Scriptable Object/Abilities/HurtBox Hit Ability", fileName = "HurtBoxHitAbility")]
public class HurtBoxHitAbility : AbilityBase {
	[SerializeField] private CombatType combatType = CombatType.Entity;

	public override void Execute(ICombatable target, EffectContext context) {
		if (target is not Component) return;
		if (context.DamageDetail.Source == null) return;

		IHurtBoxComponent hurtBox = target.HurtBox;
		if (hurtBox == null) return;

		if (context.Caster == null) return;

		hurtBox.HitEntity(context.Caster, this.combatType, context, this.effects);
	}

	protected override void HandleCastVFX(ICombatable target) {
	}

	protected override void HandleRunningVFX(ICombatable target) {
	}

	protected override void HandleSFX(ICombatable target) {
	}
}