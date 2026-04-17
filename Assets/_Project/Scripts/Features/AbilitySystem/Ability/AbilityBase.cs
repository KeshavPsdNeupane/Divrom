using System;
using Kope.Component.Combat.Interface;
using Kope.Component.Health.Interface;
using Kope.Component.HitBox.Interface;
using Kope.Component.Movement;
using Kope.Core.EntityComponentRegistry;
using UnityEngine;

public readonly struct TargetContext : IEquatable<TargetContext> {
	public readonly IHurtBoxComponent HitBox;

	public TargetContext(IHurtBoxComponent target) {
		this.HitBox = target;
	}

	public static TargetContext Create(Collider collider) {
		if (collider == null) return default;

		var registry = collider.GetComponentInParent<EntityComponentsRegistry>();
		return Create(registry);
	}

	public static TargetContext Create(Collider2D collider) {
		if (collider == null) return default;

		var registry = collider.GetComponentInParent<EntityComponentsRegistry>();
		return Create(registry);
	}

	public static TargetContext Create(EntityComponentsRegistry registry) {
		if (registry == null || registry.ComponentRegistry == null) return default;
		// these can be null, and that's fine - the TargetContext can represent a target 
		// that isn't healable, stunnable, or knockbackable without issue.
		// # dispose the return bool using _ since we don't actually need to know if 
		// the component was found or not - the resulting TargetContext will
		// just have null for any missing components, which is fine.
		// and it is abiility responsibility to check if the target is valid for its purposes,
		// not the responsibility of this TargetContext struct.
		_ = registry.ComponentRegistry.TryGetReadOnlyComponent(out IHurtBoxComponent combatTarget, false);
		if (combatTarget == null) return default;

		return new TargetContext(combatTarget);
	}



	public static TargetContext Create(IHealable healableTarget, IStunnable stunnableTarget = null) {
		if (healableTarget is not IHurtBoxComponent combatTarget) return default;
		return new TargetContext(combatTarget);
	}
	public bool Equals(TargetContext other) {
		// Two contexts are equal if their primary targets are the same object.
		// We use ReferenceEquals because these are interface references on the same GameObject.
		return ReferenceEquals(HitBox, other.HitBox);
	}

	public override bool Equals(object obj) => obj is TargetContext other && Equals(other);

	public override int GetHashCode() {
		return HitBox != null ? HitBox.GetHashCode() : 0;
	}

	public static bool operator ==(TargetContext left, TargetContext right) => left.Equals(right);
	public static bool operator !=(TargetContext left, TargetContext right) => !left.Equals(right);
}


[Serializable]
public abstract class AbilityBase : ScriptableObject {
	[SerializeField, Tooltip("Audio clip to play when the ability is cast.")]
	protected AudioClip castSfx;
	[SerializeField, Tooltip("Visual effect to play when the ability is cast.")]
	protected GameObject castVfx;
	[SerializeField, Tooltip("Visual effect to play while the ability is active.")]
	protected GameObject runningVfx;

	public AudioClip CastSfx => this.castSfx;
	public GameObject CastVfx => this.castVfx;
	public GameObject RunningVfx => this.runningVfx;

	public abstract void Execute(TargetContext target, EffectContext casterEffectContext);
	protected abstract void HandleCastVFX(TargetContext target);
	protected abstract void HandleRunningVFX(TargetContext target);
	protected abstract void HandleSFX(TargetContext target);

}