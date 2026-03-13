using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using Kope.Core.CompilerServices;
using Kope.Core.Init;


namespace Kope.Character.Stats
{
	public enum CharacterStatType { HP, ATK, DEF, MATK, SPD, CRATE, CDMG, }
	public enum DamageType
	{
		Physical,
		Fire,
		Ice,
		Lightning,
		Poison,
	}

	public class CharacterStatsSystem : InitializableBase
	{
		[SerializeField] private string characterName = "DefaultCharacter";
		private Dictionary<CharacterStatType, AdvanceStat> currentStats;
		private Dictionary<DamageType, StatBase> resistanceStats;
		private Dictionary<CharacterStatType, float> levelIncreasingStatWithLevelingValue;

		[SerializeField] private CharacterStatsSO characterStateSo;


		public string CharacterName => this.characterName;
		public Dictionary<CharacterStatType, AdvanceStat> CurrentStats => this.currentStats;
		public Dictionary<DamageType, StatBase> ResistanceStats => this.resistanceStats;

		public override bool OnInit()
		{
			try
			{
				this.currentStats ??= new Dictionary<CharacterStatType, AdvanceStat>();
				this.resistanceStats ??= new Dictionary<DamageType, StatBase>();
				this.levelIncreasingStatWithLevelingValue ??= new Dictionary<CharacterStatType, float>();
				// calling it here since, i  havent implemented any event system for world load yet
				OnFirstWorldLoad();
				return true;
			}
			catch (System.Exception ex)
			{
				MyLogger.Error($"CharacterStatsSystem initialization failed: {ex.Message}" + GetParentGameObjectHeirarchyMessage());
				return false;
			}

		}

		private void OnFirstWorldLoad()
		{
			foreach (var kvp in this.characterStateSo.BasestatsDict)
			{ this.currentStats[kvp.Key] = new AdvanceStat(kvp.Value); }

			foreach (var kvp in this.characterStateSo.ResistanceStatsDict)
			{ this.resistanceStats[kvp.Key] = new StatBase(kvp.Value); }

			var levelingStats = characterStateSo.GetLevelingStatsWithoutZero();
			foreach (var kvp in levelingStats)
			{ this.levelIncreasingStatWithLevelingValue[kvp.Key] = kvp.Value; }
		}

		private void OnEnable()
		{
			if (this.currentStats == null || this.resistanceStats == null) return;

			foreach (var stat in this.currentStats.Values)
				stat?.OnEnable();

			foreach (var stat in this.resistanceStats.Values)
				stat?.OnEnable();
		}

		private void OnDisable()
		{
			if (this.currentStats == null || this.resistanceStats == null) return;

			foreach (var stat in this.currentStats.Values)
				stat?.OnDisable();

			foreach (var stat in this.resistanceStats.Values)
				stat?.OnDisable();
		}

		protected override void OnUpdate()
		{
			base.OnUpdate();
			if (this.currentStats == null || this.resistanceStats == null) return;

			foreach (var stat in this.currentStats.Values)
				stat?.Update();

			foreach (var stat in this.resistanceStats.Values)
				stat?.Update();
		}


		public void StatsSubscribe(CharacterStatType type, UnityAction<float> callback)
		{
			if (currentStats.TryGetValue(type, out AdvanceStat stat))
				stat.OnStatsModified += callback;
		}

		public void StatsUnsubscribe(CharacterStatType type, UnityAction<float> callback)
		{
			if (currentStats.TryGetValue(type, out AdvanceStat stat))
				stat.OnStatsModified -= callback;
		}

		public void ResistanceSubscribe(DamageType type, UnityAction<float> callback)
		{
			if (resistanceStats.TryGetValue(type, out StatBase stat))
				stat.OnStatsModified += callback;
		}

		public void ResistanceUnsubscribe(DamageType type, UnityAction<float> callback)
		{
			if (resistanceStats.TryGetValue(type, out StatBase stat))
				stat.OnStatsModified -= callback;
		}

		public float GetStatValue(CharacterStatType type)
		{
			if (this.currentStats.TryGetValue(type, out AdvanceStat stat))
				return stat.GetValue();

			MyLogger.Warn($"Stat {type} not found!");
			return 0f;
		}

		public float GetResistanceValue(DamageType type)
		{
			if (this.resistanceStats.TryGetValue(type, out StatBase stat))
				return stat.GetValue();

			MyLogger.Warn($"Resistance {type} not found!");
			return 0f;
		}

		public bool AddStatModifier(StatusEffect effect)
		{
			if (this.currentStats.TryGetValue(effect.statType, out AdvanceStat stat))
				return stat.AddStatusEffect(effect);

			MyLogger.Warn($"Stat {effect.statType} not found for adding modifier!");
			return false;
		}



		public void TriggerLevelUp()
		{
			foreach (var kvp in this.levelIncreasingStatWithLevelingValue)
			{
				if (this.currentStats.TryGetValue(kvp.Key, out AdvanceStat stat))
					stat.LevelUpStat(kvp.Value);
				else
					MyLogger.Warn($"Stat {kvp.Key} not found for leveling up!");
			}
		}

		public void AddPointToStat(CharacterStatType type, float points)
		{
			if (this.currentStats.TryGetValue(type, out AdvanceStat stat))
			{
				stat.AddPointStat(points);
			}
			else
			{
				MyLogger.Warn($"Stat {type} not found for adding points!");
			}
		}

	}
}