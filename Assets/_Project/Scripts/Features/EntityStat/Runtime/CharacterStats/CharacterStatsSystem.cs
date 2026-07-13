using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

using Kope.Core.LifeTimeManagement;
using System;


namespace Kope.Character.Stats {
	public enum CharacterStatType : short {
		/// <summary>
		/// Health Points. Determines maximum life capacity. 
		/// The character is defeated when this value hits zero.
		/// </summary>
		HP = 0,

		/// <summary>
		/// Attack Power. Dictates the raw base damage dealt by physical abilities and basic attacks.
		/// </summary>
		ATK = 1,

		/// <summary>
		/// Defense. Provides flat or percentage mitigation to reduce incoming physical damage.
		/// </summary>
		DEF = 2,

		/// <summary>
		/// Intelligence. Scales the effectiveness of magical damage spells and healing output.
		/// </summary>
		INT = 3,

		/// <summary>
		/// Speed. Primarily increases tactical movement velocity, with a minor, secondary influence on attack rate.
		/// </summary>
		SPD = 4,

		/// <summary>
		/// Critical Hit Rate. The percentile probability (0% to 100%) of an attack landing a critical strike.
		/// </summary>
		CRATE = 5,

		/// <summary>
		/// Critical Hit Damage. An additive percentage multiplier applied to successful critical strikes.
		/// <para>Formula: Total Damage = Base Damage * (1.0 + (CDMG / 100)). Example: 50% CDMG increases 100 base damage to 150.</para>
		/// </summary>
		CDMG = 6,
	}
	// these are hidden stat that are not directly shown to the player but 
	// can affect gameplay in various ways, such as increasing healing received, 
	// improving resource gathering speed, or providing a luck bonus that can influence random events.
	public enum UtilityStatType : short {
		HEAL_RATE = 0,    // Incoming Multiplier
		REGEN_RATE = 10,   // Passive HP recovery
		GATHER_SPEED = 20, // Resource speed
		LUCK = 30,         // RNG Modifier
	}


	public enum StatChangeEventType {
		LevelChange = 0,
		PerkChange = 10,
		ModifierChange = 20,
	}



	public enum DamageType { Physical, Fire, Ice, Lightning, Poison, }

	public class CharacterStatsSystemBase : InitializableBase, IStatSystem, IUpdatable {

		/*
		No need to save the base stat values because they are already defined in the ScriptableObject 
		and will be loaded from there when the game starts.
		If player levels up, we can just grab the level up number from "levelComponent" and trigger 
		thelevel up function in the "AdvanceStat" which will handle the rest of the logic for 
		increasing the stats based on the level up values defined in the ScriptableObject as well.
		As for the modifiers, if they are from enemies or environment, we don't need to save 
		them because they are temporary and will be gone once the player exits the game.
		and as for the modifiers from armor, we can just save the currently equipped armor and when loading the game,
		 we can just reapply the modifiers from the equipped armor.
		*/
		private Dictionary<CharacterStatType, AdvanceStat> currentStats;
		private Dictionary<DamageType, StatBase> resistanceStats;
		private Dictionary<CharacterStatType, float> levelIncreasingStatWithLevelingValue;

		[SerializeField] private CharacterStatsSO config;

		private int currentLevel = 1;
		public Dictionary<CharacterStatType, AdvanceStat> CurrentStats => this.currentStats;
		public Dictionary<DamageType, StatBase> ResistanceStats => this.resistanceStats;

		protected override bool OnInit() {
			if (this.config == null) {
				Debug.LogError($"CharacterStatsSO is not assigned in CharacterStatsSystem.+{this.HieararchyPath}");
				return false;
			}

			this.currentStats ??= new Dictionary<CharacterStatType, AdvanceStat>();
			this.resistanceStats ??= new Dictionary<DamageType, StatBase>();
			this.levelIncreasingStatWithLevelingValue ??= new Dictionary<CharacterStatType, float>();

			// setting the default values from the ScriptableObject just as a fallback/default,
			// but later will be overridden by the save data if there is any.
			SetDefault();
			return true;

		}

		private void SetDefault() {
			foreach (var kvp in this.config.BasestatsDict) { this.currentStats[kvp.Key] = new AdvanceStat(kvp.Value); }

			foreach (var kvp in this.config.ResistanceStatsDict) { this.resistanceStats[kvp.Key] = new StatBase(kvp.Value); }

			var levelingStats = config.GetLevelingStatsWithoutZero();
			foreach (var kvp in levelingStats) { this.levelIncreasingStatWithLevelingValue[kvp.Key] = kvp.Value; }
		}

		private void OnEnable() {
			if (this.currentStats == null || this.resistanceStats == null) return;

			foreach (var stat in this.currentStats.Values)
				stat?.OnEnable();

			foreach (var stat in this.resistanceStats.Values)
				stat?.OnEnable();
		}

		private void OnDisable() {
			if (this.currentStats == null || this.resistanceStats == null) return;

			foreach (var stat in this.currentStats.Values)
				stat?.OnDisable();

			foreach (var stat in this.resistanceStats.Values)
				stat?.OnDisable();
		}

		public void OnUpdate() {
			if (this.currentStats == null || this.resistanceStats == null) return;

			foreach (var stat in this.currentStats.Values)
				stat?.Update();

			foreach (var stat in this.resistanceStats.Values)
				stat?.Update();
		}


		public void StatsSubscribe(CharacterStatType type, UnityAction<float> callback) {
			if (currentStats.TryGetValue(type, out AdvanceStat stat))
				stat.OnStatsModified += callback;
		}

		public void StatsUnsubscribe(CharacterStatType type, UnityAction<float> callback) {
			if (currentStats.TryGetValue(type, out AdvanceStat stat))
				stat.OnStatsModified -= callback;
		}

		public void ResistanceSubscribe(DamageType type, UnityAction<float> callback) {
			if (resistanceStats.TryGetValue(type, out StatBase stat))
				stat.OnStatsModified += callback;
		}

		public void ResistanceUnsubscribe(DamageType type, UnityAction<float> callback) {
			if (resistanceStats.TryGetValue(type, out StatBase stat))
				stat.OnStatsModified -= callback;
		}

		public float GetStatValue(CharacterStatType type) {
			if (this.currentStats.TryGetValue(type, out AdvanceStat stat))
				return stat.GetValue();

			Debug.LogWarning($"Stat {type} not found!");
			return 0f;
		}

		public float GetResistanceValue(DamageType type) {
			if (this.resistanceStats.TryGetValue(type, out StatBase stat))
				return stat.GetValue();

			Debug.LogWarning($"Resistance {type} not found!");
			return 0f;
		}

		public bool AddStatModifier(BaseStatModifier effect) {
			if (this.currentStats.TryGetValue(effect.statType, out AdvanceStat stat))
				return stat.AddStatusEffect(effect);

			Debug.LogWarning($"Stat {effect.statType} not found for adding modifier!");
			return false;
		}

		public void InitialLevelSetup(int level) {
			if (level <= 1) {
				this.currentLevel = 1;
				return;
			}
			var initialStats = this.config.BasestatsDict;
			int levelsGained = level - 1;
			ApplyStatGrowth((statType, growthPerLevel) => {
				float finalBaseValue = initialStats[statType] + (growthPerLevel * levelsGained);
				return finalBaseValue;
			}, (stat, calculatedValue) => stat.SetBaseValue(calculatedValue));
			this.currentLevel = level;
		}

		public void LevelUp(int newLevel) {
			int levelDifference = newLevel - this.currentLevel;
			if (levelDifference <= 0) {
				Debug.LogWarning($"LevelUp called with newLevel {newLevel} which is not greater than currentLevel {this.currentLevel}. No level up applied.");
				return;
			}
			ApplyStatGrowth((statType, growthPerLevel) => {
				float totalGrowth = growthPerLevel * levelDifference;
				return totalGrowth;
			}, (stat, calculatedValue) => stat.LevelUp(calculatedValue));

			this.currentLevel = newLevel;
		}

		/// <summary>
		/// Core helper that handles iteration, lookup safety, and applies 
		/// calculated growth values to the active stats.
		/// </summary>
		private void ApplyStatGrowth(Func<CharacterStatType, float, float> valueCalculator, Action<AdvanceStat, float> statApplier) {
			foreach (var kvp in this.levelIncreasingStatWithLevelingValue) {
				var statType = kvp.Key;
				float growthPerLevel = kvp.Value;
				if (this.currentStats.TryGetValue(statType, out AdvanceStat stat)) {
					float calculatedValue = valueCalculator(statType, growthPerLevel);

					statApplier(stat, calculatedValue);
				} else {
					Debug.LogWarning($"Stat {statType} not found in current stats!");
				}
			}
		}

		public void AddPointToStat(CharacterStatType type, float points) {
			if (this.currentStats.TryGetValue(type, out AdvanceStat stat)) {
				stat.AddPointStat(points);
			} else {
				Debug.LogWarning($"Stat {type} not found for adding points!");
			}
		}


		public bool AddResistanceModifier(ResistanceStatModifier modifier) {
			if (this.resistanceStats.TryGetValue(modifier.statType, out StatBase stat))
				return stat.AddModifier(modifier);

			Debug.LogWarning($"Resistance {modifier.statType} not found for adding modifier!");
			return false;
		}



		public void RemoveAllStatModifiers() {
			foreach (var stat in this.currentStats.Values)
				stat.RemoveAllModifiers();

			foreach (var stat in this.resistanceStats.Values)
				stat.RemoveAllModifiers();
		}

	}
}