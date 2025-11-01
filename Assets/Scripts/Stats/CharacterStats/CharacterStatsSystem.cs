using UnityEngine;
using UnityEngine.Serialization;

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
public enum DamageType
{
    Physical,
    Magical,
    Poison,
}

public class CharacterStatsSystem : MonoBehaviour
{
    [SerializeField, HideInInspector] public SerializableDictionary<CharacterStatType, Stat> currentStats;
    [SerializeField, HideInInspector] public SerializableDictionary<DamageType, Stat> resistanceStats;
    [SerializeField, HideInInspector] public SerializableDictionary<CharacterStatType, float> levelIncreasingStatWithLevelingValue;

     [SerializeField] private CharacterStatsSO characterStateSo;

    private void Awake()
    {
         this.currentStats ??= new SerializableDictionary<CharacterStatType, Stat>();
         this.resistanceStats ??= new SerializableDictionary<DamageType, Stat>();
         this.levelIncreasingStatWithLevelingValue ??= new SerializableDictionary<CharacterStatType, float>();

        OnFirstWorldLoad();
    }
    private void OnFirstWorldLoad()
    {
        foreach (var kvp in  this.characterStateSo.Basestats)
            this. currentStats[kvp.Key] = new Stat(kvp.Value);

        foreach (var kvp in  this.characterStateSo.resistanceStats)
             this.resistanceStats[kvp.Key] = new Stat(kvp.Value);

        var levelingStats = characterStateSo.GetLevelingStatsWithoutZero();
        foreach (var kvp in levelingStats)
            this. levelIncreasingStatWithLevelingValue[kvp.Key] = kvp.Value;
    }
    
    private void OnEnable()
    {
        foreach (var stat in  this.currentStats.Values)
            stat?.OnEnable();

        foreach (var stat in  this.resistanceStats.Values)
            stat?.OnEnable();
    }

    private void OnDisable()
    {
        foreach (var stat in  this.currentStats.Values)
            stat?.OnDisable();

        foreach (var stat in  this.resistanceStats.Values)
            stat?.OnDisable();
    }

    private void Update()
    {
        foreach (var stat in this.currentStats.Values)
            stat?.currentStat.Update();

        foreach (var stat in this.resistanceStats.Values)
            stat?.currentStat.Update();
    }
    
    public float GetStatValue(CharacterStatType type)
    {
        if ( this.currentStats.TryGetValue(type, out Stat stat))
            return stat.GetValue();

        Debug.LogWarning($"Stat {type} not found!");
        return 0f;
    }

    public float GetResistanceValue(DamageType type)
    {
        if ( this.resistanceStats.TryGetValue(type, out Stat stat))
            return stat.GetValue();

        Debug.LogWarning($"Resistance {type} not found!");
        return 0f;
    }

    public bool AddStatModifier(StatusEffect effect)
    {
        if ( this.currentStats.TryGetValue(effect.statType, out Stat stat))
            return stat.currentStat.AddModifier(effect);

        Debug.LogWarning($"Stat {effect.statType} not found for adding modifier!");
        return false;
    }

    public void TriggerLevelUp()
    {
        foreach (var kvp in  this.levelIncreasingStatWithLevelingValue)
        {
            if ( this.currentStats.TryGetValue(kvp.Key, out Stat stat))
                stat.levelingStat.LevelUp(kvp.Value);
            else
                Debug.LogWarning($"Stat {kvp.Key} not found for leveling up!");
        }
    }


}
