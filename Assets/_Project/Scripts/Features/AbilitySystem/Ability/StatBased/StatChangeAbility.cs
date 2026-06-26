using System.Collections.Generic;
using Kope.AbilitySystem;
using Kope.Character.Stats;
using Kope.Component.Combat.Interface;
using Kope.Component.HitBox.Interface;
using UnityEngine;
using ZLinq;


[CreateAssetMenu(menuName = "Scriptable Objects/Abilities/Stat Change Ability", fileName = "StatChangeAbility")]
public class StatChangeAbility : AbilityBase {
	[SerializeField] private HitTargetType applicableHitTargetType = HitTargetType.Entity;
	[SerializeField] private List<StatChangeEffectSetting> statChangeEffectSetting;
	private List<IEffectFactory<IStatSystem>> _cachedStatChangeEffects = new();

	public override void Initialize() {
		this._cachedStatChangeEffects = this.statChangeEffectSetting?.AsValueEnumerable()
			.Select((s, i) => {
				var factory = s.GetFactory();
				if (factory == null)
					Debug.LogWarning($"[StatChangeAbility] Index {i} returned null factory on '{this.name}'.", this);
				return factory;
			})
			.Where(f => f != null)
			.ToList() ?? new();
	}


	public override void Execute(TargetContext target, EffectContext context) {
		if (target.HitBox == null || context.Caster == null) return;
		if (target.HitBox.CombatType != this.applicableHitTargetType) return;
		var position = GetTargetPosition(target);
		SpawnRunningVfx(position);
		var hitbox = target.HitBox;
		hitbox.HitStatChange(context, target.HitBox.CombatType, this._cachedStatChangeEffects);
	}
}
