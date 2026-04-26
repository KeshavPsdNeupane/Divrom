using System;
using Kope.Component.Ability.Targeting;
using Kope.Component.Combat.Interface;
using Kope.Component.HitBox.Interface;
using Kope.Core.Attribute;
using UnityEngine;
public class TargetContext {
	public readonly IHitBoxComponent HitBox;

	public TargetContext(IHitBoxComponent target) {
		this.HitBox = target;
	}
	public static TargetContext Create(Component component) {
		if (component == null) return null;


		if (component.TryGetComponent<IHitBoxComponent>(out var hitBox)) {
			return new TargetContext(hitBox);
		}

		return null;
	}
}
[Serializable]
public abstract class AbilityBase : ScriptableObject, ITargetingReceiver {
	[SerializeField] string abilityName;
	[SerializeField, ReadOnly] string abilityID;
	[SerializeField, Tooltip("Audio clip to play when the ability is cast.")]
	protected AudioClip castSfx;
	[SerializeField, Tooltip("Visual effect to play when the ability is cast.")]
	protected GameObject castVfx;
	[SerializeField, Tooltip("Visual effect to play while the ability is active.")]
	protected GameObject runningVfx;

	[SerializeField] protected TargetingSettings targetingSettings;

	private int _abilityUsedCount = 0;
	public int AbilityUsedCount => this._abilityUsedCount;
	public string AbilityName => this.abilityName;
	public string AbilityID => this.abilityID;
	public AudioClip CastSfx => this.castSfx;
	public GameObject CastVfx => this.castVfx;
	public GameObject RunningVfx => this.runningVfx;

	public bool IsInstantCast {
		get {
			// if the targeting strategy is self targeting, then we can consider it an instant cast ability
			// since it doesn't require any additional input for targeting, and can just be triggered immediately upon selection.
			return this.targetingSettings.selectedType == TargetingType.SelfTargeting;
		}
	}

	public string GetSaveData() {
		return $"{this.abilityID}:{this._abilityUsedCount}";
	}
	public void IncrementAbilityUsedCount() => this._abilityUsedCount++;
	public void InjectAbilityUsedCount(int count) => this._abilityUsedCount = count;


	public virtual void Cast(
		   TargetingManager targetingManager,
			TargetContext casterContext,
		   EffectContext effectContext) {
		//var strategy = targetingFactory?.Create() ?? new SelfTargetingStrategy();


		// lets see if the new enum based binding system works correctly, this should be able to
		// get the correct targeting strategy based on the selected enum value in the editor, 
		// without needing to hardcode any logic for each specific strategy in the ability class,
		// and it should also automatically instantiate the strategy if it hasn't been created yet, 
		// which is a nice bonus feature that reduces boilerplate and makes it easier to manage the 
		// targeting strategies for each ability.
		var strategy = this.targetingSettings.GetFactory()?.Create() ?? new SelfTargetingStrategy();

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
			this
		);
	}

	public abstract void Execute(TargetContext target, EffectContext context);


	public void OnTargetingResolved(TargetContext target, EffectContext context) {
		Execute(target, context);
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

		// only run on editor since this is for maintaining the 
		// unique ID for each ability asset, which is used for saving and loading the ability state,
		// and we don't want this code running in a build since it relies on UnityEditor APIs 
		// that aren't available in a build.
#if UNITY_EDITOR
		var path = UnityEditor.AssetDatabase.GetAssetPath(this);
		// no early exit allowed since we will miss the Enable call to initialize
		// child class variables.
		if (!string.IsNullOrEmpty(path)) {
			var existingGuid = UnityEditor.AssetDatabase.AssetPathToGUID(path);
			if (existingGuid != this.abilityID) {
				this.abilityID = existingGuid;
				UnityEditor.EditorUtility.SetDirty(this);
			}
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