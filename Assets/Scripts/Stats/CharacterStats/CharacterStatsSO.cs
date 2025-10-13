using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterStateSO", menuName = "Scriptable Character/CharacterStateSO")]
public class CharacterStatsSO : ScriptableObject
{
  [SerializeField]  public SerializableDictionary<CharacterStatType, int> Basestats = new SerializableDictionary<CharacterStatType, int>();

    [SerializeField] public SerializableDictionary<CharacterResistanceType, int> resistanceStats = new SerializableDictionary<CharacterResistanceType, int>();

    [SerializeField] public SerializableDictionary<CharacterStatType, int> levelIncreasingStatWithLevelingValue = new SerializableDictionary<CharacterStatType, int>();

    private void OnEnable()
    {
        if (Basestats.Count == 0)
        {
            Basestats.Add(CharacterStatType.Health, 100);
            Basestats.Add(CharacterStatType.Defense, 10);
            Basestats.Add(CharacterStatType.Attack, 15);
            Basestats.Add(CharacterStatType.MagicAttack, 12);
            Basestats.Add(CharacterStatType.MovementSpeed, 5);
            Basestats.Add(CharacterStatType.CriticalRate, 5);
            Basestats.Add(CharacterStatType.CriticalDamage, 100);
        }

        if (resistanceStats.Count == 0)
        {
            resistanceStats.Add(CharacterResistanceType.Physical, 5);
            resistanceStats.Add(CharacterResistanceType.Magical, 3);
            resistanceStats.Add(CharacterResistanceType.Poison, 0);
        }

        if (levelIncreasingStatWithLevelingValue.Count == 0)
        {
            levelIncreasingStatWithLevelingValue.Add(CharacterStatType.Health, 10);
            levelIncreasingStatWithLevelingValue.Add(CharacterStatType.Defense, 1);
            levelIncreasingStatWithLevelingValue.Add(CharacterStatType.Attack, 2);
            levelIncreasingStatWithLevelingValue.Add(CharacterStatType.MagicAttack, 0);
            levelIncreasingStatWithLevelingValue.Add(CharacterStatType.MovementSpeed, 0);
            levelIncreasingStatWithLevelingValue.Add(CharacterStatType.CriticalRate, 0);
            levelIncreasingStatWithLevelingValue.Add(CharacterStatType.CriticalDamage, 0);
        }
    }

    public Dictionary<CharacterStatType, int> GetLevelingStatsWithoutZero()
    {
        var filtered = new Dictionary<CharacterStatType, int>();
        foreach (var kvp in levelIncreasingStatWithLevelingValue)
        {
            if (kvp.Value != 0)
                filtered.Add(kvp.Key, kvp.Value);
        }
        return filtered;
    }
}
