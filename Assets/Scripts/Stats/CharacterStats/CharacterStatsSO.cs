using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterStateSO", menuName = "Scriptable Character/CharacterStateSO")]
public class CharacterStatsSO : ScriptableObject
{
  [SerializeField]  public SerializableDictionary<CharacterStatType, float> Basestats =
        new SerializableDictionary<CharacterStatType, float>();

    [SerializeField] public SerializableDictionary<CharacterResistanceType, float> resistanceStats = 
        new SerializableDictionary<CharacterResistanceType, float >();

    [SerializeField] public SerializableDictionary<CharacterStatType, float> levelIncreasingStatWithLevelingValue = 
        new SerializableDictionary<CharacterStatType, float>();

    private void OnEnable()
    {
        if (this.Basestats.Count == 0)
        {
          this. Basestats?.Add(CharacterStatType.Health, 100f);
            this.Basestats?.Add(CharacterStatType.Defense, 10f);
            this.Basestats?.Add(CharacterStatType.Attack, 15f);
            this.Basestats?.Add(CharacterStatType.MagicAttack, 12f);
            this.Basestats?.Add(CharacterStatType.MovementSpeed, 5f);
            this.Basestats?.Add(CharacterStatType.CriticalRate, 5f);
            this.Basestats?.Add(CharacterStatType.CriticalDamage, 100f);
        }

        if (resistanceStats.Count == 0)
        {
            this.resistanceStats?.Add(CharacterResistanceType.Physical, 5f);
            this.resistanceStats?.Add(CharacterResistanceType.Magical, 3f);
            this.resistanceStats?.Add(CharacterResistanceType.Poison, 0f);
        }

        if (this.levelIncreasingStatWithLevelingValue.Count == 0)
        {
            this.levelIncreasingStatWithLevelingValue?.Add(CharacterStatType.Health, 10f);
            this.levelIncreasingStatWithLevelingValue?.Add(CharacterStatType.Defense, 1f);
            this.levelIncreasingStatWithLevelingValue?.Add(CharacterStatType.Attack, 2f);
            this.levelIncreasingStatWithLevelingValue?.Add(CharacterStatType.MagicAttack, 0f);
            this.levelIncreasingStatWithLevelingValue?.Add(CharacterStatType.MovementSpeed, 0f);
            this.levelIncreasingStatWithLevelingValue?.Add(CharacterStatType.CriticalRate, 0f);
            this.levelIncreasingStatWithLevelingValue?.Add(CharacterStatType.CriticalDamage, 0f);
        }
    }

    public SerializableDictionary<CharacterStatType, float> GetLevelingStatsWithoutZero()
    {
        var filtered = new SerializableDictionary<CharacterStatType, float>();
        foreach (var kvp in this.levelIncreasingStatWithLevelingValue)
        {
            if (kvp.Value > 0f)
                filtered?.Add(kvp.Key, kvp.Value);
        }
        return filtered;
    }
}
