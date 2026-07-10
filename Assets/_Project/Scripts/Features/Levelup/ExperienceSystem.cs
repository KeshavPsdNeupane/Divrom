using Kope.Character.Stats;
using Kope.Core.EntityComponentRegistry;
using Kope.Core.LifeTimeManagement;
using Kope.ExperienceSystem.Config;
using Kope.ExperienceSystem.Interface;
using UnityEngine;

namespace Kope.ExperienceSystem {
	public class ExperienceSystem : InitializableBase, IExperienceSystem {
		[Header("Level System Config")]
		[SerializeField, Min(1)] private int defaultLevel = 1;
		[SerializeField] private LevelSystemConfig levelSystemConfig;
		[SerializeField] private EntityComponentsRegistry ecr;
		[Header("Batching Settings")]
		[SerializeField, Tooltip("How often to check for level-ups (in seconds)")]
		private float batchCheckInterval = 0.2f;
		[SerializeField, Tooltip("Delay before the first level-up check (in seconds)")]
		private float batchInitialCheckDelay = 2f;

		private IStatSystem _statsSystem;

		private float _currentExp = 0f;
		private int _currentLevel = 1;
		private float _lastCheckedExp = 0f;

		public event System.Action<int> OnLevelChanged;
		public float CurrentExp => this._currentExp;
		public int CurrentLevel => this._currentLevel;


		protected override bool OnInit() {
			if (this.levelSystemConfig == null) {
				Debug.LogError($"LevelSystemConfig is not assigned in LevelSystem.+{GetParentGameObjectHeirarchyMessage()}");
				return false;
			}
			if (this.ecr == null) {
				Debug.LogError($"EntityComponentsRegistry is not assigned in LevelSystem.+{GetParentGameObjectHeirarchyMessage()}");
				return false;
			}
			if (!this.ecr.ComponentRegistry.TryGetReadOnlyComponent(out this._statsSystem)) {
				Debug.LogError($"Failed to find IStatSystem in EntityComponentsRegistry.+{GetParentGameObjectHeirarchyMessage()}");
				return false;
			}
			Default();
			InvokeRepeating(nameof(HandleLevelUpBatching), this.batchInitialCheckDelay, this.batchCheckInterval);
			return true;
		}


		private void Default() {
			this._currentLevel = this.defaultLevel;
			this._currentExp = this.levelSystemConfig.GetCumulativeXpForLevel(this._currentLevel);
		}

		private void OnEnable() {
			LevelChangeEvent(this._statsSystem.LevelUp, true);
		}
		private void OnDisable() {
			LevelChangeEvent(this._statsSystem.LevelUp, false);
		}

		/// <summary>
		/// Adds experience points to the current experience total. If the amount is less 
		/// than or equal to zero, the method will exit without making any changes.
		/// It waits for the next batch check to determine if a level-up has occurred, 
		/// rather than checking immediately.
		/// </summary>
		/// <param name="amount"></param>
		public void AddExperience(float amount) {
			if (amount <= 0) return;
			this._currentExp += amount;
		}


		private void HandleLevelUpBatching() {
			if (this._currentExp == this._lastCheckedExp) return;

			this._lastCheckedExp = this._currentExp;

			int newLevel = this.levelSystemConfig.GetLevelFromCumulativeXp(this._currentExp);
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
		public void LevelChangeEvent(System.Action<int> callback, bool isSubscribe) {
			if (isSubscribe) {
				this.OnLevelChanged -= callback;
				this.OnLevelChanged += callback;
			} else {
				this.OnLevelChanged -= callback;
			}
		}


		protected override void OnDestroy() {
			// first child then parent.
			CancelInvoke(nameof(HandleLevelUpBatching));
			base.OnDestroy();
		}



		public void SimulateLevelUp() {
			// i know i can just add and mutate the data for the simulation, but i want to 
			// make sure the level system is working as intended, so i will use the 
			// actual method to add experience and let the batching handle the level up.
			float newLevelExp = this.levelSystemConfig.GetCumulativeXpForLevel(this._currentLevel + 1);
			this.AddExperience(newLevelExp - this._currentExp);
		}
	}

}