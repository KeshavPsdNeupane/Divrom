using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using Kope.Core.CompilerServices;
using Kope.Core.Init;


namespace Kope.Character.Stats {
	public enum CharacterStatType {
		HP, // Health Points
		ATK, // Attack Power
		DEF, // Defense
		SP, // Spell Power, affects magic damage and healing power
		AGI, // Agility, affects movement speed and attack speed(just give minor/negligible boost to attack speed, main purpose is to increase movement speed)
		CRATE, // Critical Hit Rate
		CDMG, // Critical Hit Damage
	}
	// these are hidden stat that are not directly shown to the player but 
	// can affect gameplay in various ways, such as increasing healing received, 
	// improving resource gathering speed, or providing a luck bonus that can influence random events.
	public enum UtilityStatType {
		HEAL_RATE,    // Incoming Multiplier
		REGEN_RATE,   // Passive HP recovery
		GATHER_SPEED, // Resource speed
		LUCK,         // RNG Modifier
	}

	public enum DamageType { Physical, Fire, Ice, Lightning, Poison, }

	public class CharacterStatsSystem : InitializableBase, IStatSystem {

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
		[SerializeField] private string characterName = "DefaultCharacter";
		private Dictionary<CharacterStatType, AdvanceStat> currentStats;
		private Dictionary<DamageType, StatBase> resistanceStats;
		private Dictionary<CharacterStatType, float> levelIncreasingStatWithLevelingValue;

		[SerializeField] private CharacterStatsSO characterStateSo;


		public string CharacterName => this.characterName;
		public Dictionary<CharacterStatType, AdvanceStat> CurrentStats => this.currentStats;
		public Dictionary<DamageType, StatBase> ResistanceStats => this.resistanceStats;

		protected override bool OnInit() {
			try {
				this.currentStats ??= new Dictionary<CharacterStatType, AdvanceStat>();
				this.resistanceStats ??= new Dictionary<DamageType, StatBase>();
				this.levelIncreasingStatWithLevelingValue ??= new Dictionary<CharacterStatType, float>();

				// setting the default values from the ScriptableObject just as a fallback/default,
				// but later will be overridden by the save data if there is any.
				SetDefault();
				return true;
			} catch (System.Exception ex) {
				MyLogger.Error($"CharacterStatsSystem initialization failed: {ex.Message}" + GetParentGameObjectHeirarchyMessage());
				return false;
			}

		}

		private void SetDefault() {
			foreach (var kvp in this.characterStateSo.BasestatsDict) { this.currentStats[kvp.Key] = new AdvanceStat(kvp.Value); }

			foreach (var kvp in this.characterStateSo.ResistanceStatsDict) { this.resistanceStats[kvp.Key] = new StatBase(kvp.Value); }

			var levelingStats = characterStateSo.GetLevelingStatsWithoutZero();
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

		protected override void OnUpdate() {
			base.OnUpdate();
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

			MyLogger.Warn($"Stat {type} not found!");
			return 0f;
		}

		public float GetResistanceValue(DamageType type) {
			if (this.resistanceStats.TryGetValue(type, out StatBase stat))
				return stat.GetValue();

			MyLogger.Warn($"Resistance {type} not found!");
			return 0f;
		}

		public bool AddStatModifier(BaseStatModifier effect) {
			if (this.currentStats.TryGetValue(effect.statType, out AdvanceStat stat))
				return stat.AddStatusEffect(effect);

			MyLogger.Warn($"Stat {effect.statType} not found for adding modifier!");
			return false;
		}

		public void TriggerLevelUp() {
			foreach (var kvp in this.levelIncreasingStatWithLevelingValue) {
				if (this.currentStats.TryGetValue(kvp.Key, out AdvanceStat stat))
					stat.LevelUpStat(kvp.Value);
				else
					MyLogger.Warn($"Stat {kvp.Key} not found for leveling up!");
			}
		}

		public void AddPointToStat(CharacterStatType type, float points) {
			if (this.currentStats.TryGetValue(type, out AdvanceStat stat)) {
				stat.AddPointStat(points);
			} else {
				MyLogger.Warn($"Stat {type} not found for adding points!");
			}
		}



		public bool AddResistanceModifier(ResistanceStatModifier modifier) {
			if (this.resistanceStats.TryGetValue(modifier.statType, out StatBase stat))
				return stat.AddModifier(modifier);

			MyLogger.Warn($"Resistance {modifier.statType} not found for adding modifier!");
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