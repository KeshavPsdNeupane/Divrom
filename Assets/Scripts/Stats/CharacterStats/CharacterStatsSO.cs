using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterStateSO", menuName = "Scriptable Character/CharacterStateSO")]
public class CharacterStatsSO : ScriptableObject
{
    [SerializeField]
    public SerializableDictionary<CharacterStatType, float> Basestats =
        new();

    [SerializeField]
    public SerializableDictionary<DamageType, float> resistanceStats =
         new();

    [SerializeField]
    public SerializableDictionary<CharacterStatType, float> levelIncreasingStatWithLevelingValue =
        new();

    private void OnEnable()
    {
        // Base stats
        AddIfMissing(Basestats, CharacterStatType.Health, 100f);
        AddIfMissing(Basestats, CharacterStatType.Defense, 10f);
        AddIfMissing(Basestats, CharacterStatType.Attack, 15f);
        AddIfMissing(Basestats, CharacterStatType.MagicAttack, 12f);
        AddIfMissing(Basestats, CharacterStatType.MovementSpeed, 5f);
        AddIfMissing(Basestats, CharacterStatType.CriticalRate, 5f);
        AddIfMissing(Basestats, CharacterStatType.CriticalDamage, 100f);

        // Resistance stats (auto-adds missing ones)
        AddIfMissing(resistanceStats, DamageType.Physical, 5f);
        AddIfMissing(resistanceStats, DamageType.Fire, 5f);
        AddIfMissing(resistanceStats, DamageType.Ice, 5f);
        AddIfMissing(resistanceStats, DamageType.Lightning, 5f);
        AddIfMissing(resistanceStats, DamageType.Poison, 5f);

        // Leveling stats
        AddIfMissing(levelIncreasingStatWithLevelingValue, CharacterStatType.Health, 10f);
        AddIfMissing(levelIncreasingStatWithLevelingValue, CharacterStatType.Defense, 1f);
        AddIfMissing(levelIncreasingStatWithLevelingValue, CharacterStatType.Attack, 2f);
        AddIfMissing(levelIncreasingStatWithLevelingValue, CharacterStatType.MagicAttack, 0f);
        AddIfMissing(levelIncreasingStatWithLevelingValue, CharacterStatType.MovementSpeed, 0f);
        AddIfMissing(levelIncreasingStatWithLevelingValue, CharacterStatType.CriticalRate, 0f);
        AddIfMissing(levelIncreasingStatWithLevelingValue, CharacterStatType.CriticalDamage, 0f);


        FillMissingEnumKeys(Basestats);
        FillMissingEnumKeys(resistanceStats);
        FillMissingEnumKeys(levelIncreasingStatWithLevelingValue);
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

    private void AddIfMissing<T>(SerializableDictionary<T, float> dict, T key, float value)
    {
        if (!dict.ContainsKey(key))
            dict.Add(key, value);
    }

    private void FillMissingEnumKeys<T>(SerializableDictionary<T, float> dict, float defaultValue = 0f) where T : System.Enum
    {
        foreach (T key in System.Enum.GetValues(typeof(T)))
        {
            if (!dict.ContainsKey(key))
                dict[key] = defaultValue;
        }
    }
}

