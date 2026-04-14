using Kope.Component.Combat.Interface;
using UnityEngine;

[CreateAssetMenu(menuName = "Scriptable Object/Abilities/TestAbility", fileName = "TestAbility")]
public class TestAbility : AbilityBase {
	public override void Execute(ICombatable target, EffectContext context) {
		// np op, just being used so the effect are being serialized and can be tested in the inspector for now.
	}

	protected override void HandleCastVFX(ICombatable target) {
		// no op read above --- IGNORE ---
	}

	protected override void HandleRunningVFX(ICombatable target) {
		// no op read above --- IGNORE ---
	}

	protected override void HandleSFX(ICombatable target) {
		// no op read above --- IGNORE ---
	}
}
