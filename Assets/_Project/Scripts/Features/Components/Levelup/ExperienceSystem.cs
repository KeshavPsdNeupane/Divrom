using Kope.Character.Stats;
using Kope.Component.ExperienceSystem.Config;
using Kope.Component.ExperienceSystem.Interface;
using Kope.Core.EntityComponentRegistry;
using Kope.Core.Collections;
using UnityEngine;
using Kope.EntityComponentSystem;
using Kope.SaveSystem;
using Kope.SaveSystem.Attributes;
using Newtonsoft.Json;

namespace Kope.Component.ExperienceSystem {
	[SaveComponentData("experience_data")]
	public class ExperienceSystemSaveData : ISaveData {
		// why i am not using the AccumulatorInt directly? Because the save system 
		// shouldnt depend on the implementation of the accumulator, and this way we 
		// can change it later without breaking save data.
		[JsonProperty("value")] public int Value;
		[JsonProperty("residual")] public float Residual;
		public ExperienceSystemSaveData(int value, float residual) {
			this.Value = value;
			this.Residual = residual;
		}
	}

	[SaveComponent("exp_system")]
	public class ExperienceSystem : ComponentBase, IExperienceSystem, ISaveable {
		[Header("Level System Config")]
		[SerializeField, Min(1)] private int defaultLevel = 1;
		[SerializeField] private ExperienceSystemConfig config;
		[SerializeField] private EntityComponentsRegistry ecr;

		[Header("Batching Settings")]
		[SerializeField, Tooltip("How often to check for level-ups (in seconds)")]
		private float batchCheckInterval = 0.2f;
		[SerializeField, Tooltip("Delay before the first level-up check (in seconds)")]
		private float batchInitialCheckDelay = 2f;

		private IStatSystem _statsSystem;

		/// <summary>
		/// Single source of truth tracking total cumulative lifetime experience.
		/// Utilizes an AccumulatorInt to safely process floating-point micro-rewards 
		/// via a residual buffer while storing an unshakeable whole integer.
		/// Bypasses high-level 32-bit float precision degradation bugs completely.
		/// </summary>
		private AccumulatorInt _currentExp = AccumulatorInt.Default;

		private int _currentLevel = 1;
		private int _lastCheckedExp = 0;

		public event System.Action<int> OnLevelChanged;

		// Implicit operators inside AccumulatorInt map these automatically to clear ints
		public int CurrentExp => this._currentExp;
		public int CurrentLevel => this._currentLevel;


		protected override bool OnInit() {
			if (this.config == null) {
				Debug.LogError($"ExperienceSystemConfig is not assigned in ExperienceSystem.+{this.HieararchyPath}");
				return false;
			}
			if (this.ecr == null) {
				Debug.LogError($"EntityComponentsRegistry is not assigned in ExperienceSystem.+{this.HieararchyPath}");
				return false;
			}
			if (!this.ecr.TryFetchReadOnly(this, this.HieararchyPath, out this._statsSystem)) {
				return false;
			}

			Default();
			this._statsSystem.InitialLevelSetup(this._currentLevel);
			// invoke the level changed event to ensure any subscribers are aware of the initial level state
			// Runs a low-frequency heart-beat loop for performance. Keeps expensive level-up math and 
			// string/UI events separated from frame-rate performance entirely.
			InvokeRepeating(nameof(HandleLevelUpBatching), this.batchInitialCheckDelay, this.batchCheckInterval);
			return true;
		}


		private void Default() {
			this._currentLevel = this.defaultLevel;
			// Seed the accumulator cleanly using the absolute configuration milestone milestone requirement
			this._currentExp = new AccumulatorInt(this.config.GetCumulativeXpForLevel(this._currentLevel) + 1);
		}

		private void OnEnable() {
			OnLevelChangeEvent(this._statsSystem.LevelUp, true);
		}
		private void OnDisable() {
			OnLevelChangeEvent(this._statsSystem.LevelUp, false);
		}



		public ISaveData GetSaveData() {
			return new ExperienceSystemSaveData(
				this._currentExp,
				this._currentExp.Residual
			);
		}

		public void LoadFromSaveData(ISaveData data) {
			if (data is ExperienceSystemSaveData saveData) {
				this._currentExp = new AccumulatorInt(saveData.Value, saveData.Residual);
			}
		}


		/// <summary>
		/// Adds experience points to the running total. Supports fractional values.
		/// The overloaded operators in AccumulatorInt automatically catch fractions, 
		/// process thresholds with Banker's Rounding, and retain leftovers under the hood.
		/// Actual progression shifts are batched to prevent mid-frame calculation stutter.
		/// </summary>
		/// <param name="amount">The quantity of experience to offer (fractions supported).</param>
		public void AddExperience(float amount) {
			if (amount <= 0f) return;
			this._currentExp += amount;
		}


		/// <summary>
		/// Evaluates progression state at set intervals. If cumulative integer changes are 
		/// detected, uses a rapid Binary Search lookup against the ScriptableObject table 
		/// to check if a valid Level shift occurred.
		/// </summary>
		private void HandleLevelUpBatching() {
			// Early out if the integer representation hasn't altered since the last tick
			if (this._currentExp == this._lastCheckedExp) return;

			this._lastCheckedExp = this._currentExp;

			int newLevel = this.config.GetLevelFromCumulativeXp(this._currentExp);
			if (newLevel != this._currentLevel) {
				this._currentLevel = newLevel;
				this.OnLevelChanged?.Invoke(this._currentLevel);
			}
		}

		/// <summary>
		/// Subscribes or unsubscribes a callback to the level change event.
		/// </summary>
		/// <remarks>
		/// Reuses a single method with a boolean toggle to simplify event lifecycle management. 
		/// To ensure safe event handling and prevent duplicate registrations, it automatically 
		/// unsubscribes the callback before performing a new subscription.
		/// </remarks>
		/// <param name="callback">The method to invoke with the new level value when a level change occurs.</param>
		/// <param name="isSubscribe">True to register the callback; False to remove it.</param>
		public void OnLevelChangeEvent(System.Action<int> callback, bool isSubscribe) {
			if (isSubscribe) {
				this.OnLevelChanged -= callback;
				this.OnLevelChanged += callback;
			} else {
				this.OnLevelChanged -= callback;
			}
		}


		protected override void OnDestroy() {
			CancelInvoke(nameof(HandleLevelUpBatching));
			base.OnDestroy();
		}


		/// <summary>
		/// Forces an exact progression shift to the next level milestone.
		/// Adds the precise delta required to clear the current bracket via AddExperience 
		/// to ensure all native underlying structures register the advancement reliably.
		/// </summary>
		public void SimulateLevelUp() {
			int newLevelExp = this.config.GetCumulativeXpForLevel(this._currentLevel + 1) + 1;
			AddExperience(newLevelExp - this._currentExp);
		}


	}
}