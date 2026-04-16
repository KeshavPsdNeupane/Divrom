using System;
using Kope.Component.Combat.Interface;
using Kope.Component.Health.Interface;
using Kope.Component.Movement;
using Kope.Core.EntityComponentRegistry;
using UnityEngine;

public readonly struct TargetContext : IEquatable<TargetContext> {
	public readonly ICombatable DamageTarger;
	public readonly IHealable HealableTarget;
	public readonly IStunnable StunnableTarget;
	public readonly IKnockbackable KnockbackableTarget;

	public TargetContext(ICombatable target,
	IHealable healableTarget = null, IStunnable stunnableTarget = null,
	IKnockbackable knockbackableTarget = null) {

		this.DamageTarger = target;
		this.HealableTarget = healableTarget;
		this.StunnableTarget = stunnableTarget;
		this.KnockbackableTarget = knockbackableTarget;
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

		registry.ComponentRegistry.TryGetReadOnlyComponent<IDamageProcessor>(out var target, false);
		return Create(registry, target);
	}

	private static TargetContext Create(EntityComponentsRegistry registry, IDamageProcessor target) {
		if (registry == null || registry.ComponentRegistry == null || target == null) return default;

		IHealable healableTarget;
		IStunnable stunnableTarget;
		IKnockbackable knockbackableTarget;
		// ignore the return values since we're using TryGetReadOnlyComponent to avoid 
		// unnecessary logging of missing components
		_ = registry.ComponentRegistry.TryGetReadOnlyComponent(out healableTarget, false);
		_ = registry.ComponentRegistry.TryGetReadOnlyComponent(out stunnableTarget, false);
		_ = registry.ComponentRegistry.TryGetReadOnlyComponent(out knockbackableTarget, false);
		return new TargetContext(target, healableTarget, stunnableTarget, knockbackableTarget);
	}

	public static TargetContext Create(IHealable healableTarget, IStunnable stunnableTarget = null) {
		if (healableTarget is not ICombatable combatTarget) return default;
		return new TargetContext(combatTarget, healableTarget, stunnableTarget);
	}
	public bool Equals(TargetContext other) {
		// Two contexts are equal if their primary targets are the same object.
		// We use ReferenceEquals because these are interface references on the same GameObject.
		return ReferenceEquals(DamageTarger, other.DamageTarger) &&
			   ReferenceEquals(HealableTarget, other.HealableTarget) &&
			   ReferenceEquals(StunnableTarget, other.StunnableTarget) &&
			   ReferenceEquals(KnockbackableTarget, other.KnockbackableTarget);
	}

	public override bool Equals(object obj) => obj is TargetContext other && Equals(other);

	public override int GetHashCode() {
		return HashCode.Combine(DamageTarger, HealableTarget, StunnableTarget, KnockbackableTarget);
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