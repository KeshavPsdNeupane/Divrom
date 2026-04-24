using System;
using Kope.Component.Ability.Targeting;
using Kope.Component.Combat.Interface;
using Kope.Component.HitBox.Interface;
using Kope.Core.Attribute;
using Kope.Core.Attributes;
using Kope.Core.EntityComponentRegistry;
using UnityEngine;

public class TargetContext {
	public readonly IHitBoxComponent HitBox;

	public TargetContext(IHitBoxComponent target) {
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
		_ = registry.ComponentRegistry.TryGetReadOnlyComponent(out IHitBoxComponent combatTarget, false);
		if (combatTarget == null) return default;

		return new TargetContext(combatTarget);
	}
}

[Serializable]
public abstract class AbilityBase : ScriptableObject {
	[SerializeField] string abilityName;
	[SerializeField, ReadOnly] string abilityID;
	[SerializeField, Tooltip("Audio clip to play when the ability is cast.")]
	protected AudioClip castSfx;
	[SerializeField, Tooltip("Visual effect to play when the ability is cast.")]
	protected GameObject castVfx;
	[SerializeField, Tooltip("Visual effect to play while the ability is active.")]
	protected GameObject runningVfx;
	[SerializeReference, SubclassSelector] protected ITargetingFactory targetingFactory;

	private int _abilityUsedCount = 0;
	public int AbilityUsedCount => this._abilityUsedCount;

	public string AbilityName => this.abilityName;
	public string AbilityID => this.abilityID;
	public AudioClip CastSfx => this.castSfx;
	public GameObject CastVfx => this.castVfx;
	public GameObject RunningVfx => this.runningVfx;

	/// <summary>
	/// Whether this ability requires explicit input to be cast, or if it can be triggered 
	/// automatically by the system when the ability is selected. 
	/// For example, a passive ability that triggers automatically when certain conditions 
	/// are met would return false here, while an active ability that the player needs
	/// to manually trigger would return true.
	/// Or any ability that apply to the caster itself and doesn't require targeting, 
	/// such as a self heal or a buff, could return false here since it can
	/// just be triggered immediately upon selection without needing additional 
	/// input for targeting.
	/// </summary>
	public bool IsInstantCast => this.targetingFactory == null || this.targetingFactory is SelfTargetingStrategy;
	public string GetSaveData() {
		return $"{this.abilityID}:{this._abilityUsedCount}";
	}
	public void IncrementAbilityUsedCount() => this._abilityUsedCount++;
	public void InjectAbilityUsedCount(int count) => this._abilityUsedCount = count;


	public abstract void Execute(TargetContext target, EffectContext context);

	public virtual void Cast(
		   TargetingManager targetingManager,
		   in TargetContext casterContext,
		   EffectContext effectContext) {
		var strategy = targetingFactory?.Create() ?? new SelfTargetingStrategy();
		// here we can play the casting sfx and vfx immediately upon casting
		// # cast vfx/sfx that will be played when the ability is cast, before the targeting is resolved. 
		// this is for things like a fireball that shoots out immediately when you cast, even if it doesn't 
		// hit anything.
		var position = effectContext.Caster != null ? effectContext.Caster.transform.position : Vector3.zero;
		// popullating the ability count in the effect context so that it can be used by effects for scaling 
		// or other purposes, such as "next level scaling" effects that become stronger after 
		// using the ability a certain number of times.

		/// the ability will level up even without hitting anything,
		// since the count is incremented on cast, not on hit, so that the player 
		// is rewarded for using the ability regardless of whether it connects.
		effectContext.AbilityUsedCount = this._abilityUsedCount++;
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

	protected static Vector3 GetTargetPosition(TargetContext target) {
		if (target.HitBox is Component c) {
			return c.transform.position;
		}
		return Vector3.zero;
	}






	#region  Editor Only - 
	/// <summary>
	/// OnEnable is called when the asset is loaded in the editor, which can happen when the project is
	///  opened or when scripts are recompiled.
	/// We use it here to ensure that the abilityID is consistent with the asset's GUID in the Unity Editor. 
	/// this is important because the abilityID is used for saving and loading the state of abilities, 
	/// and it needs to be consistent with the asset's GUID to ensure that the correct ability state 
	/// is loaded for each asset.<br/>
	/// Use Enable() method to add any additional editor-only initialization logic for the ability asset,
	/// such as setting up default values or ensuring that certain components are present. This method will
	/// be called automatically when the asset is loaded in the editor, allowing you to prepare the 
	/// ability asset for use and ensure that it is in a valid state before it is used in the game
	/// </summary>
	private void OnEnable() {
#if UNITY_EDITOR
		// only run on editor since this is for maintaining the 
		// unique ID for each ability asset, which is used for saving and loading the ability state,
		// and we don't want this code running in a build since it relies on UnityEditor APIs 
		// that aren't available in a build.
		var path = UnityEditor.AssetDatabase.GetAssetPath(this);
		if (string.IsNullOrEmpty(path)) return; // not yet saved to disk, skip

		var existingGuid = UnityEditor.AssetDatabase.AssetPathToGUID(path);
		if (existingGuid != this.abilityID) {
			this.abilityID = existingGuid;
			UnityEditor.EditorUtility.SetDirty(this);
		}
#endif
		Enable();
	}

	/// <summary>
	/// Called when the asset is enabled, in both editor and builds.
	/// Override to add initialization logic for the ability asset,
	/// such as setting up default values or ensuring that certain 
	/// components are present.
	/// </summary>
	protected virtual void Enable() {
		// no op
	}
	#endregion


}