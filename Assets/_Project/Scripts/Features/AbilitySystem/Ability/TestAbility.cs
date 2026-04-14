using Kope.Component.HurtBox.Interface;
using UnityEngine;

[CreateAssetMenu(menuName = "Scriptable Object/Abilities/TestAbility", fileName = "TestAbility")]
public class TestAbility : AbilityBase {
	public override void Execute(IDamageable target, EffectContext context) {
		// np op, just being used so the effect are being serialized and can be tested in the inspector for now.
	}

	protected override void HandleCastVFX(IDamageable target) {
		// no op read above --- IGNORE ---
	}

	protected override void HandleRunningVFX(IDamageable target) {
		// no op read above --- IGNORE ---
	}

	protected override void HandleSFX(IDamageable target) {
		// no op read above --- IGNORE ---
	}
}
