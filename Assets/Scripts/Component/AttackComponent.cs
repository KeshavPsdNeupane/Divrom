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
        if (this.statsSystem == null)
        {
            Debug.LogWarning("CharacterStatsSystem not assigned in AttackComponent, trying to get it from the GameObject.");
            this.statsSystem = GetComponent<CharacterStatsSystem>();
        }

    }
    private void Start()
    {
        // this func is also here to ensure subscription in case OnEnable is missed
        // and if we disable and enable the component later, we still
        // want to subscribe to the stats updates in OnEnable   
        Subscribe();
    }
    void OnEnable()
    {
        // Checking So all the stats exist and are initialized
        if (this.statsSystem != null &&
            this.statsSystem.currentStats != null &&
            this.statsSystem.currentStats.ContainsKey(CharacterStatType.ATK))
        {
            Subscribe();
        }

    }

    void OnDisable()
    {
        this.statsSystem.StatsUnsubscribe(CharacterStatType.ATK, AttackCallback);
        this.statsSystem.StatsUnsubscribe(CharacterStatType.CRATE, CriticalRateCallback);
        this.statsSystem.StatsUnsubscribe(CharacterStatType.CDMG, CalculateDamage);
    }
    private void AttackCallback(float attack) => this.attack = attack;

    private void CriticalRateCallback(float criticalChance)
     => this.normalizedCriticalChance = criticalChance / 100f;
    private void CalculateDamage(float criticalDamage)
     => this.normalizedCriticalDamage = (100 + criticalDamage) / 100f;


    private void Subscribe()
    {


        this.statsSystem.StatsSubscribe(CharacterStatType.ATK, AttackCallback);
        this.statsSystem.StatsSubscribe(CharacterStatType.CRATE, CriticalRateCallback);
        this.statsSystem.StatsSubscribe(CharacterStatType.CDMG, CalculateDamage);

        AttackCallback(this.statsSystem.currentStats[CharacterStatType.ATK].GetValue());
        CriticalRateCallback(this.statsSystem.currentStats[CharacterStatType.CRATE].GetValue());
        CalculateDamage(this.statsSystem.currentStats[CharacterStatType.CDMG].GetValue());

    }




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
