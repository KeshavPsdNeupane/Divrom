using System.Collections.Generic;
using Kope.AbilitySystem.Effect.Settings;
using Kope.Component.Combat.Interface;
using Kope.Component.Health.Interface;
using Kope.Component.HitBox.Interface;
using UnityEngine;
using ZLinq;

[CreateAssetMenu(menuName = "Scriptable Objects/Abilities/Heal Ability", fileName = "HealAbility")]
public class HealAbility : AbilityBase {
	[SerializeField] private HitTargetType applicableHitTargetType = HitTargetType.Entity;
	[SerializeField] private List<HealEffectSetting> healEffectSetting;
	private List<IEffectFactory<IHealable>> _cachedHealEffects = new();

	protected override void Enable() {
		this._cachedHealEffects = this.healEffectSetting?.AsValueEnumerable()
			.Select((s, i) => {
				var factory = s.GetFactory();
				if (factory == null)
					// 1000% not possible of being null since the whole drawer fall back to very first option if 
					// the selected one is null, but just in case to avoid null reference exception later on
					//  when executing the ability, we log a warning here.
					Debug.LogWarning($"[HealAbility] Index {i} returned null factory on '{this.name}'.", this);
				return factory;
			})
			.Where(f => f != null)
			.ToList() ?? new();

	}

	public override void Execute(TargetContext target, EffectContext context) {
		if (target.HitBox == null || context.Caster == null) return;
		if (target.HitBox.CombatType != this.applicableHitTargetType) return;

		var position = GetTargetPosition(target);
		// running mean vfx that will be applied when hit to certain area or target, position is
		// where the vfx will be spawned, can be used by abilities that want to spawn a vfx on the 
		// ground at the target location, or a vfx that follows the target around while the ability is active
		SpawnRunningVfx(position);
		var hitbox = target.HitBox;
		hitbox.HitHealable(context, target.HitBox.CombatType, this._cachedHealEffects);
	}
}
