using System.Collections.Generic;
using ZLinq;
using Kope.AbilitySystem.Effect.Settings;
using Kope.Component.Combat.Interface;
using Kope.Component.HitBox.Interface;
using UnityEngine;

[CreateAssetMenu(menuName = "Scriptable Objects/Abilities/Damage Ability", fileName = "DamageAbility")]
public class DamageAbility : AbilityBase {
	[SerializeField] private HitTargetType applicableHitTargetType = HitTargetType.Entity;
	[SerializeField] private List<DamageEffectSetting> damageEffectSettings;
	private List<IEffectFactory<IDamagable>> _cachedEffectFactories = new();
	protected override void Enable() {
		this._cachedEffectFactories = this.damageEffectSettings?.AsValueEnumerable()
			.Select((s, i) => {
				var factory = s.GetFactory();
				if (factory == null)
					Debug.LogWarning($"[DamageAbility] Index {i} returned null factory on '{this.name}'.", this);
				return factory;
			})
			.Where(f => f != null)
			.ToList() ?? new();
	}

	public override void Execute(TargetContext target, EffectContext context) {
		if (target.HitBox == null || context.Caster == null) return;
		if (target.HitBox.CombatType != this.applicableHitTargetType) return;
		// running mean vfx that will be applied when hit to certain area or target, position is
		// where the vfx will be spawned, can be used by abilities that want to spawn a vfx on the 
		// ground at the target location, or a vfx that follows the target around while the ability is active
		var position = GetTargetPosition(target);
		SpawnRunningVfx(position);

		target.HitBox.HitCombatible(context, target.HitBox.CombatType, this._cachedEffectFactories);
	}
}
