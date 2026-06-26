using System;
using Kope.Component.Ability.Targeting;
using Kope.Component.Combat.Interface;
using Kope.Component.HitBox.Interface;
using Kope.Core.Attribute;
using ThirdParty;
using UnityEditor;
using UnityEngine;

namespace Kope.AbilitySystem {
	public class TargetContext {
		public readonly IHitBoxComponent HitBox;

		public TargetContext(IHitBoxComponent target) {
			this.HitBox = target;
		}
		public static TargetContext Create(UnityEngine.Component component) {
			if (component == null) return null;


			if (component.TryGetComponent<IHitBoxComponent>(out var hitBox)) {
				return new TargetContext(hitBox);
			}

			return null;
		}
	}

	[Serializable]
	public abstract class AbilityBase : ScriptableObject {
		[SerializeField, ReadOnly] private string abilityID;

		[Space(5), Header("UI")]
		[SerializeField] private string abilityName;
		[SerializeField] private Sprite abilityIcon;

		[Space(5), Header("Effects")]
		[SerializeField, Tooltip("Audio clip to play when the ability is cast.")]
		protected AudioClip castSfx;
		[SerializeField, Tooltip("Visual effect to play when the ability is cast.")]
		protected GameObject castVfx;
		[SerializeField, Tooltip("Visual effect to play while the ability is active.")]
		protected GameObject runningVfx;

		[Space(5), Header("Targeting")]
		[SerializeField] protected TargetingSettings targetingSettings;

		[Space(5), PostSpace(20), Header("Cooldown")]
		[SerializeField] private float cooldownDuration = 5f;

		// Getters
		public string AbilityID => this.abilityID;
		public string AbilityName => this.abilityName;
		public float CooldownDuration => this.cooldownDuration;
		public TargetingSettings TargetingSettings => this.targetingSettings;

		public bool IsInstantCast {
			get {
				// if the targeting strategy is self targeting, then we can consider it an instant cast ability
				// since it doesn't require any additional input for targeting, and can just be triggered immediately upon selection.
				return this.targetingSettings.selectedType == TargetingType.SelfTargeting;
			}
		}

		/// <summary>
		/// Called once on the cloned runtime instance immediately after instantiation,
		/// before the ability is ever cast.<br/>
		/// Override to cache effect factories from your serialized settings lists.
		/// This is the correct place for any preparation that reads from serialized fields
		/// and produces runtime-ready objects — do not do this in <see cref="Execute"/> or <see cref="Enable"/>.
		/// </summary>
		public abstract void Initialize();

		public abstract void Execute(TargetContext target, EffectContext context);

		public void PlayCastSfx(Vector3 position) { if (this.castSfx != null) AudioSource.PlayClipAtPoint(this.castSfx, position); }
		public void SpawnCastVfx(Vector3 position) { if (this.castVfx != null) Instantiate(this.castVfx, position, Quaternion.identity); }
		public void SpawnRunningVfx(Vector3 position) { if (this.runningVfx != null) Instantiate(this.runningVfx, position, Quaternion.identity); }

		protected static Vector3 GetTargetPosition(TargetContext target) {
			if (target.HitBox is UnityEngine.Component c) {
				return c.transform.position;
			}
			return Vector3.zero;
		}

		/// <summary>
		/// Editor-only. Keeps <see cref="abilityID"/> in sync with the asset's GUID whenever
		/// the asset is loaded (project open, script recompile, asset reimport).<br/>
		/// The ID must match the GUID so that save data correctly maps back to the right ability asset.
		/// For runtime initialization, see <see cref="Initialize"/>.
		/// </summary>
		private void OnEnable() {
#if UNITY_EDITOR
			var path = AssetDatabase.GetAssetPath(this);
			if (!string.IsNullOrEmpty(path)) {
				var existingGuid = AssetDatabase.AssetPathToGUID(path);
				if (existingGuid != this.abilityID) {
					this.abilityID = existingGuid;
					EditorUtility.SetDirty(this);
				}
			}
#endif
		}
	}


	public class AbilityRuntime : ITargetingReceiver {
		public AbilityBase Config { get; private set; }
		private TargetingStrategy _currentTargetingStrategy;
		private BasicCountDownTimer _abilityCooldownTimer;
		private int _abilityUsedCount = 0;
		public int AbilityUsedCount => this._abilityUsedCount;
		public bool CanCast => !this._abilityCooldownTimer.IsRunning;
		public bool IsInstantCast => this.Config.IsInstantCast;
		public BasicCountDownTimer CooldownDuration => this._abilityCooldownTimer;
		public float CooldownRemaining => this._abilityCooldownTimer.Time;
		public AbilityRuntime(AbilityBase config, int abilityUsedCount = 0) {
			this.Config = UnityEngine.Object.Instantiate(config);
			this.Config.Initialize();
			this._abilityUsedCount = abilityUsedCount;
			this._abilityCooldownTimer = new BasicCountDownTimer(this.Config.CooldownDuration);
		}

		public string GetSaveData() {
			return $"{Config.AbilityID}:{this._abilityUsedCount}";
		}

		/// <summary>
		/// Restores the ability's used count from saved data. This is useful for persisting the 
		/// state of the ability across game sessions, ensuring that any scaling or effects based 
		/// on usage are accurately maintained.
		/// </summary>
		/// <param name="count"></param>
		public void RestoreAbilityUsedCount(int count) => this._abilityUsedCount = count;

		public virtual void TickCooldowns(float deltaTime) {
			this._abilityCooldownTimer.Tick(deltaTime);
		}

		public virtual void Cast(
			TargetingManager targetingManager,
			TargetContext casterContext,
			EffectContext effectContext) {
			// for single it internally caches the selected factory,

			this._currentTargetingStrategy = Config.TargetingSettings.GetFactory().Create() ?? new SelfTargetingStrategy();
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
			Config.PlayCastSfx(position);
			Config.SpawnCastVfx(position);
			this._currentTargetingStrategy.Start(
				targetingManager,
				casterContext,
				effectContext,
				this
			);
		}

		public void Cancel() {
			this._currentTargetingStrategy?.FinishTheStrategy(true);
			this._currentTargetingStrategy = null;
		}


		public void OnTargetingResolved(TargetContext target, EffectContext context) {
			this._abilityCooldownTimer.Start();
			Config.Execute(target, context);
		}
	}
}