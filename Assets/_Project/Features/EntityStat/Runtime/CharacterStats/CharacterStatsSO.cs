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

    private void Awake()
    {
        // Base stats
        AddIfMissing(Basestats, CharacterStatType.HP, 100f);
        AddIfMissing(Basestats, CharacterStatType.DEF, 10f);
        AddIfMissing(Basestats, CharacterStatType.ATK, 15f);
        AddIfMissing(Basestats, CharacterStatType.MATK, 12f);
        AddIfMissing(Basestats, CharacterStatType.SPD, 5f);
        AddIfMissing(Basestats, CharacterStatType.CRATE, 5f);
        AddIfMissing(Basestats, CharacterStatType.CDMG, 100f);

        // Resistance stats (auto-adds missing ones)
        AddIfMissing(resistanceStats, DamageType.Physical, 5f);
        AddIfMissing(resistanceStats, DamageType.Fire, 5f);
        AddIfMissing(resistanceStats, DamageType.Ice, 5f);
        AddIfMissing(resistanceStats, DamageType.Lightning, 5f);
        AddIfMissing(resistanceStats, DamageType.Poison, 5f);

        // Leveling stats
        AddIfMissing(levelIncreasingStatWithLevelingValue, CharacterStatType.HP, 10f);
        AddIfMissing(levelIncreasingStatWithLevelingValue, CharacterStatType.DEF, 1f);
        AddIfMissing(levelIncreasingStatWithLevelingValue, CharacterStatType.ATK, 2f);
        AddIfMissing(levelIncreasingStatWithLevelingValue, CharacterStatType.MATK, 0f);
        AddIfMissing(levelIncreasingStatWithLevelingValue, CharacterStatType.SPD, 0f);
        AddIfMissing(levelIncreasingStatWithLevelingValue, CharacterStatType.CRATE, 0f);
        AddIfMissing(levelIncreasingStatWithLevelingValue, CharacterStatType.CDMG, 0f);


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

