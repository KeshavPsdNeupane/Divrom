using Unity.VisualScripting;
using UnityEngine;

public class AttackComponent : MonoBehaviour
{
    [SerializeField] private CharacterStatsSystem statsSystem;
    private float attack;
    private float normalizedCriticalChance;
    private float normalizedCriticalDamage;

    void Awake()
    {
        if (statsSystem == null)
        {
            statsSystem = GetComponent<CharacterStatsSystem>();
        }

    }
    private void Start()
    {
        this.statsSystem.StatsSubscribe(CharacterStatType.Attack, AttackCallback);
        this.statsSystem.StatsSubscribe(CharacterStatType.CriticalRate, CriticalRateCallback);
        this.statsSystem.StatsSubscribe(CharacterStatType.CriticalDamage, CalculateDamage);

        // Initial fetch
        AttackCallback(this.statsSystem.currentStats[CharacterStatType.Attack].GetValue());
        CriticalRateCallback(this.statsSystem.currentStats[CharacterStatType.CriticalRate].GetValue());
        CalculateDamage(this.statsSystem.currentStats[CharacterStatType.CriticalDamage].GetValue());

    }
    void OnDisable()
    {
        this.statsSystem.StatsUnsubscribe(CharacterStatType.Attack, AttackCallback);
        this.statsSystem.StatsUnsubscribe(CharacterStatType.CriticalRate, CriticalRateCallback);
        this.statsSystem.StatsUnsubscribe(CharacterStatType.CriticalDamage, CalculateDamage);
    }
    private void AttackCallback(float attack) => this.attack = attack;

    private void CriticalRateCallback(float criticalChance)
     => this.normalizedCriticalChance = criticalChance / 100f;
    private void CalculateDamage(float criticalDamage)
     => this.normalizedCriticalDamage = (100 + criticalDamage) / 100f;


    private float CalculateDamage()
    {
        float damage = this.attack;
        if (normalizedCriticalChance >= 1f) { return damage * this.normalizedCriticalDamage; }

        bool isCriticalHit = Random.value < this.normalizedCriticalChance;
        if (isCriticalHit)
        {
            return damage * this.normalizedCriticalDamage;
        }
        return damage;
    }

}
