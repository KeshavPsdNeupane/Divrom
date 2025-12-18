using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

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
    private Dictionary<CharacterStatType, Stat> currentStats;
    private Dictionary<DamageType, Stat> resistanceStats;
    private Dictionary<CharacterStatType, float> levelIncreasingStatWithLevelingValue;

    [SerializeField] private CharacterStatsSO characterStateSo;


    public Dictionary<CharacterStatType, Stat> CurrentStats => this.currentStats;
    public Dictionary<DamageType, Stat> ResistanceStats => this.resistanceStats;


    public override void Init()
    {
        this.currentStats ??= new Dictionary<CharacterStatType, Stat>();
        this.resistanceStats ??= new Dictionary<DamageType, Stat>();
        this.levelIncreasingStatWithLevelingValue ??= new Dictionary<CharacterStatType, float>();

        OnFirstWorldLoad();
        SetInitialized();
    }

    private void OnFirstWorldLoad()
    {
        foreach (var kvp in this.characterStateSo.Basestats)
            this.currentStats[kvp.Key] = new Stat(kvp.Value);

        foreach (var kvp in this.characterStateSo.resistanceStats)
            this.resistanceStats[kvp.Key] = new Stat(kvp.Value);

        var levelingStats = characterStateSo.GetLevelingStatsWithoutZero();
        foreach (var kvp in levelingStats)
            this.levelIncreasingStatWithLevelingValue[kvp.Key] = kvp.Value;
    }

    private void OnEnable()
    {
        foreach (var stat in this.currentStats.Values)
            stat?.OnEnable();

        foreach (var stat in this.resistanceStats.Values)
            stat?.OnEnable();
    }

    private void OnDisable()
    {
        foreach (var stat in this.currentStats.Values)
            stat?.OnDisable();

        foreach (var stat in this.resistanceStats.Values)
            stat?.OnDisable();
    }

    private void Update()
    {
        foreach (var stat in this.currentStats.Values)
            stat?.CurrentStat.Update();

        foreach (var stat in this.resistanceStats.Values)
            stat?.CurrentStat.Update();
    }


    public void StatsSubscribe(CharacterStatType type, UnityAction<float> callback)
    {
        if (currentStats.TryGetValue(type, out Stat stat))
            stat.OnStatsModified += callback;
    }

    public void StatsUnsubscribe(CharacterStatType type, UnityAction<float> callback)
    {
        if (currentStats.TryGetValue(type, out Stat stat))
            stat.OnStatsModified -= callback;
    }

    public void ResistanceSubscribe(DamageType type, UnityAction<float> callback)
    {
        if (resistanceStats.TryGetValue(type, out Stat stat))
            stat.OnStatsModified += callback;
    }

    public void ResistanceUnsubscribe(DamageType type, UnityAction<float> callback)
    {
        if (resistanceStats.TryGetValue(type, out Stat stat))
            stat.OnStatsModified -= callback;
    }

    public float GetStatValue(CharacterStatType type)
    {
        if (this.currentStats.TryGetValue(type, out Stat stat))
            return stat.GetValue();

        Logger.LogWarning($"Stat {type} not found!");
        return 0f;
    }

    public float GetResistanceValue(DamageType type)
    {
        if (this.resistanceStats.TryGetValue(type, out Stat stat))
            return stat.GetValue();

        Logger.LogWarning($"Resistance {type} not found!");
        return 0f;
    }

    public bool AddStatModifier(StatusEffect effect)
    {
        if (this.currentStats.TryGetValue(effect.statType, out Stat stat))
            return stat.CurrentStat.AddModifier(effect);

        Logger.LogWarning($"Stat {effect.statType} not found for adding modifier!");
        return false;
    }

    public void TriggerLevelUp()
    {
        foreach (var kvp in this.levelIncreasingStatWithLevelingValue)
        {
            if (this.currentStats.TryGetValue(kvp.Key, out Stat stat))
                stat.LevelingStat.LevelUp(kvp.Value);
            else
                Logger.LogWarning($"Stat {kvp.Key} not found for leveling up!");
        }
    }

    public void AddPointToStat(CharacterStatType type, float points)
    {
        if (this.currentStats.TryGetValue(type, out Stat stat))
        {
            stat.PointStats.AddPointModifier(points);
        }
        else
        {
            Logger.LogWarning($"Stat {type} not found for adding points!");
        }
    }





}
