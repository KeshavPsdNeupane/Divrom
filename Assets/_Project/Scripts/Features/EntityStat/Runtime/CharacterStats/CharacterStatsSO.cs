using UnityEngine;
using Kope.Core.Collections;
using System;
using System.Collections.Generic;

namespace Kope.Character.Stats {
	[CreateAssetMenu(fileName = "CharacterStateSO", menuName = "Scriptable Character/CharacterStateSO")]
	public class CharacterStatsSO : ScriptableObject {
		[SerializeField] private SerializableDictionary<CharacterStatType, float> Basestats = new();

		[Header("Resistance Stats (-0.5 to 2.0)")]
		[SerializeField] private SerializableDictionary<DamageType, float> resistanceStats = new();

		[SerializeField] private SerializableDictionary<CharacterStatType, float> levelIncreasingStatWithLevelingValue = new();


		public SerializableDictionary<CharacterStatType, float> BasestatsDict => Basestats;
		public SerializableDictionary<DamageType, float> ResistanceStatsDict => resistanceStats;

		private void OnValidate() {
			// 1. Auto-fill missing keys with specific defaults
			FillStatsWithDefaults();

			// 2. Clamp Resistance values
			if (resistanceStats != null) {
				var keys = new List<DamageType>(resistanceStats.Keys);
				foreach (var key in keys) {
					resistanceStats[key] = Mathf.Clamp(resistanceStats[key], -0.5f, 2.0f);
				}
			}
		}

		private void FillStatsWithDefaults() {
			// Fill Base Stats
			FillMissingEnumKeys(Basestats, (stat) => stat switch {
				CharacterStatType.HP => 100f,
				CharacterStatType.DEF => 10f,
				CharacterStatType.ATK => 15f,
				CharacterStatType.INT => 12f,
				CharacterStatType.SPD => 5f,
				CharacterStatType.CRATE => 5f,
				CharacterStatType.CDMG => 100f,
				_ => 0f
			});

			// Fill Resistance (0.05f default)
			FillMissingEnumKeys(resistanceStats, (type) => 0.05f);

			// Fill Leveling Stats
			FillMissingEnumKeys(levelIncreasingStatWithLevelingValue, (stat) => stat switch {
				CharacterStatType.HP => 10f,
				CharacterStatType.DEF => 1f,
				CharacterStatType.ATK => 2f,
				_ => 0f
			});
		}

		/// <summary>
		/// Iterates through the Enum and fills missing keys using a provider function for values.
		/// </summary>
		private void FillMissingEnumKeys<T>(SerializableDictionary<T, float> dict, Func<T, float> defaultValueProvider) where T : Enum {
			foreach (T key in Enum.GetValues(typeof(T))) {
				if (!dict.ContainsKey(key)) {
					dict.Add(key, defaultValueProvider(key));
				}
			}
		}

		public SerializableDictionary<CharacterStatType, float> GetLevelingStatsWithoutZero() {
			var filtered = new SerializableDictionary<CharacterStatType, float>();
			foreach (var kvp in this.levelIncreasingStatWithLevelingValue) {
				if (kvp.Value > 0f)
					filtered.Add(kvp.Key, kvp.Value);
			}
			return filtered;
		}
	}
}