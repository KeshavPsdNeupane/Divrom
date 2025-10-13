using System;
using UnityEngine;

public enum CharacterStatType
{
    Health,
    Defense,
    Attack,
    MagicAttack,
    MovementSpeed,
    CriticalRate,
    CriticalDamage,
}
public enum CharacterResistanceType
{
    Physical,
    Magical,
    Poison,
}

public class CharacterStats : MonoBehaviour
{
    [SerializeField, HideInInspector] public SerializableDictionary<CharacterStatType, CurrentStat> currentestats;
    [SerializeField, HideInInspector] public SerializableDictionary<CharacterResistanceType, CurrentStat> resistanceStats;
    [SerializeField,HideInInspector] public SerializableDictionary<CharacterStatType, int> levelIncreasingStatWithLevelingValue;

    [SerializeField] private CharacterStatsSO characterStateSO;

    private void Awake()
    {
        // Initialize if null
        this.currentestats ??= new SerializableDictionary<CharacterStatType, CurrentStat>();
        this.resistanceStats ??= new SerializableDictionary<CharacterResistanceType, CurrentStat>();
        this.levelIncreasingStatWithLevelingValue ??= new SerializableDictionary<CharacterStatType, int>();

        OnFirstWorldLoad();
    }

    private void OnEnable()
    {

        if (this.currentestats != null) {
            foreach (var stat in currentestats.Values)
                stat.OnEnable();
        }

        if(this.resistanceStats != null) {
            foreach (var res in resistanceStats.Values)
                res?.OnEnable();
        }

    }
    private void OnDisable()
    {
        if (this.currentestats != null)
        {
            foreach (var stat in currentestats.Values)
                stat.OnDisable();
        }

        if (this.resistanceStats != null)
        {
            foreach (var res in resistanceStats.Values)
                res?.OnDisable();
        }
    }



    private void Update()
    {
        foreach (var stat in currentestats.Values)
            stat.Update();
        foreach (var res in resistanceStats.Values)
            res.Update();
    }


    public int GetStatValue(CharacterStatType type)
    {
        if (this.currentestats.TryGetValue(type, out CurrentStat stat))
            return stat.GetValue();
        Debug.LogWarning($"Stat {type.ToString()} not found!");
        return 0;
    }

    public int GetResistanceValue(CharacterResistanceType type)
    {
        if (this.resistanceStats.TryGetValue(type, out CurrentStat stat))
            return stat.GetValue();
        Debug.LogWarning($"Resistance {type.ToString()} not found!");
        return 0;
    }

    public bool AddStatModifier(StatusEffect effect)
    {
        if (this.currentestats.TryGetValue(effect.statType, out CurrentStat stat)) { return stat.AddModifier(effect); }
        Debug.LogWarning($"Stat {effect.statType.ToString()} not found for adding modifier!");
        return false;
    }


    public void TriggerLevelUp()
    {
        foreach (var kvp in this.levelIncreasingStatWithLevelingValue)
        {
            if (this.currentestats.TryGetValue(kvp.Key, out CurrentStat stat))
            {
                stat.perkAdditiveAndLevelingStat.levelingStat.LevelUp(kvp.Value);
            }
            else
            {
                Debug.LogWarning($"Stat {kvp.Key.ToString()} not found for leveling up!");
            }
        }
    }


    private void OnFirstWorldLoad()
    {
        foreach (var kvp in this.characterStateSO.Basestats)
            this.currentestats[kvp.Key] = new CurrentStat(kvp.Value);

        foreach (var kvp in this.characterStateSO.resistanceStats)
            this.resistanceStats[kvp.Key] = new CurrentStat(kvp.Value);

        var levelingStats =this. characterStateSO.GetLevelingStatsWithoutZero();
        foreach (var kvp in levelingStats)
            this.levelIncreasingStatWithLevelingValue[kvp.Key] = kvp.Value;

        this.characterStateSO = null;
    }


}
