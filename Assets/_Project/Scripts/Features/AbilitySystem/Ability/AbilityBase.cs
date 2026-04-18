using System;
using Kope.Component.Ability.Targeting;
using Kope.Component.Combat.Interface;
using Kope.Component.HitBox.Interface;
using Kope.Core.EntityComponentRegistry;
using UnityEngine;

public class TargetContext {
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

	public abstract ITargetingFactory TargetingFactory { get; }

	public abstract void Execute(TargetContext target, EffectContext context);

	public virtual void Cast(
		   TargetingManager targetingManager,
		   in TargetContext casterContext,
		   EffectContext effectContext) {
		var strategy = TargetingFactory?.Create() ?? new SelfTargetingStrategy();
		// here we can play the casting sfx and vfx immediately upon casting
		// # cast vfx/sfx that will be played when the ability is cast, before the targeting is resolved. 
		// this is for things like a fireball that shoots out immediately when you cast, even if it doesn't 
		// hit anything.
		var position = effectContext.Caster != null ? effectContext.Caster.transform.position : Vector3.zero;
		PlayCastSfx(position);
		SpawnCastVfx(position);
		strategy.Start(
			targetingManager,
			casterContext,
			effectContext,
			(target, ctx) => Execute(target, ctx)
		);
	}

	protected void PlayCastSfx(Vector3 position) {
		if (this.castSfx != null) {
			AudioSource.PlayClipAtPoint(this.castSfx, position);
		}
	}

	protected void SpawnCastVfx(Vector3 position) {
		if (this.castVfx != null) {
			Instantiate(this.castVfx, position, Quaternion.identity);
		}
	}

	protected void SpawnRunningVfx(Vector3 position) {
		if (this.runningVfx != null) {
			Instantiate(this.runningVfx, position, Quaternion.identity);
		}
	}

	protected static Vector3 GetTargetPosition(in TargetContext target) {
		if (target.HitBox is Component c) {
			return c.transform.position;
		}
		return Vector3.zero;
	}
}