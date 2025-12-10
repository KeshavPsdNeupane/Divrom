using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Component responsible for handling attack logic based on character stats.
/// Must be used In Init Lifecycle Manager.
/// </summary>
public class AttackComponent : InitializableBase
{
    [SerializeField] private CharacterStatsSystem statsSystem;
    private float attack;
    private float normalizedCriticalChance;
    private float normalizedCriticalDamage;
    public event UnityAction OnAttackPerformed;

    public override void Init()
    {
        if (this.statsSystem == null)
        {
            Debug.LogWarning("CharacterStatsSystem not assigned in AttackComponent, trying to get it from the GameObject.");
            this.statsSystem = GetComponent<CharacterStatsSystem>();
        }
        SetInitialized();

    }
    void OnEnable()
    {
        // Checking So all the stats exist and are initialized
        // Checking still in just case for someone who doesn't use the Init Lifecycle
        if (this.statsSystem != null &&
            this.statsSystem.CurrentStats != null) Subscribe();
    }

    void OnDisable() => Unsubscribe();

    private void AttackCallback(float attack) => this.attack = attack;
    private void CriticalRateCallBack(float criticalChance)
     => this.normalizedCriticalChance = criticalChance * 0.01f;
    private void CriticalDamageCallBack(float criticalDamage)
     => this.normalizedCriticalDamage = 1 + criticalDamage * 0.01f;


    private void Subscribe()
    {
        this.statsSystem.StatsSubscribe(CharacterStatType.ATK, AttackCallback);
        this.statsSystem.StatsSubscribe(CharacterStatType.CRATE, CriticalRateCallBack);
        this.statsSystem.StatsSubscribe(CharacterStatType.CDMG, CriticalDamageCallBack);
        // Initial fetch
        AttackCallback(this.statsSystem.CurrentStats[CharacterStatType.ATK].GetValue());
        CriticalRateCallBack(this.statsSystem.CurrentStats[CharacterStatType.CRATE].GetValue());
        CriticalDamageCallBack(this.statsSystem.CurrentStats[CharacterStatType.CDMG].GetValue());
    }

    private void Unsubscribe()
    {
        this.statsSystem.StatsUnsubscribe(CharacterStatType.ATK, AttackCallback);
        this.statsSystem.StatsUnsubscribe(CharacterStatType.CRATE, CriticalRateCallBack);
        this.statsSystem.StatsUnsubscribe(CharacterStatType.CDMG, CriticalDamageCallBack);
    }

    public void AttackForInputSystem(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            float damage = CalculateDamage();
            Debug.Log($"Attack performed! Damage dealt: {damage}");
            this.OnAttackPerformed?.Invoke();
        }
    }



    private float CalculateDamage()
    {
        float damage = this.attack;
        if (this.normalizedCriticalChance >= 1f) { return damage * this.normalizedCriticalDamage; }

        bool isCriticalHit = Random.value < this.normalizedCriticalChance;
        if (isCriticalHit)
        {
            return damage * this.normalizedCriticalDamage;
        }
        return damage;
    }

    private float CalculateDamage(float baseScalerStats)
    {
        float damage = baseScalerStats;
        if (this.normalizedCriticalChance >= 1f) { return damage * this.normalizedCriticalDamage; }

        bool isCriticalHit = Random.value < this.normalizedCriticalChance;
        if (isCriticalHit)
        {
            return damage * this.normalizedCriticalDamage;
        }
        return damage;
    }




}
