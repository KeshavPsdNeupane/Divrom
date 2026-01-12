using Kope.Core.CompilerServices;
using UnityEngine;
using UnityEngine.Events;
using Kope.Core.Init;

/// <summary>
/// Base attack logic component. Can be used for both player and AI.
/// Handles stat subscription and damage calculation.
/// </summary>
public abstract class AttackComponentBase : InitializableBase
{
    [SerializeField] private AnimationComponent animationComponent;
    [SerializeField] protected CharacterStatsSystem statsSystem;
    [SerializeField] WeaponSO equippedWeaponDataSO;
    protected float attack;
    protected float normalizedCriticalChance;
    protected float normalizedCriticalDamage;

    public WeaponData EquippedWeaponData => this.equippedWeaponDataSO.CurrentWeaponData;

    public event UnityAction OnAttackPerformed;

    public override void Init()
    {
        if (this.statsSystem == null)
        {
            MyLogger.Warn("CharacterStatsSystem not assigned in AttackComponentBase, trying to get it from the GameObject.");
            this.statsSystem = GetComponent<CharacterStatsSystem>();
        }
        if (this.animationComponent == null)
        {
            MyLogger.Warn("AnimationComponent not assigned in AttackComponentBase, trying to get it from the GameObject.");
            this.animationComponent = GetComponent<AnimationComponent>();
        }

        SubscribeToStats();
        SetInitialized();
    }

    protected virtual void OnEnable() => SubscribeToStats();
    protected virtual void OnDisable() => UnsubscribeFromStats();

    protected void SubscribeToStats()
    {
        if (statsSystem != null && statsSystem.CurrentStats != null)
        {
            statsSystem.StatsSubscribe(CharacterStatType.ATK, AttackCallback);
            statsSystem.StatsSubscribe(CharacterStatType.CRATE, CriticalRateCallBack);
            statsSystem.StatsSubscribe(CharacterStatType.CDMG, CriticalDamageCallBack);

            // Initial fetch
            AttackCallback(statsSystem.GetStatValue(CharacterStatType.ATK));
            CriticalRateCallBack(statsSystem.GetStatValue(CharacterStatType.CRATE));
            CriticalDamageCallBack(statsSystem.GetStatValue(CharacterStatType.CDMG));
        }
    }

    protected void UnsubscribeFromStats()
    {
        if (statsSystem != null && statsSystem.CurrentStats != null)
        {
            statsSystem.StatsUnsubscribe(CharacterStatType.ATK, AttackCallback);
            statsSystem.StatsUnsubscribe(CharacterStatType.CRATE, CriticalRateCallBack);
            statsSystem.StatsUnsubscribe(CharacterStatType.CDMG, CriticalDamageCallBack);
        }
    }

    protected virtual void AttackCallback(float value) => this.attack = value;
    protected virtual void CriticalRateCallBack(float value) => this.normalizedCriticalChance = value * 0.01f;
    protected virtual void CriticalDamageCallBack(float value) => this.normalizedCriticalDamage = 1 + value * 0.01f;

    protected float CalculateDamage()
    {
        return CalculateDamage(this.attack);
    }
    protected float CalculateDamage(float baseScalingStat)
    {
        float damage = baseScalingStat;
        if (normalizedCriticalChance >= 1f) return damage * normalizedCriticalDamage;

        if (Random.value < normalizedCriticalChance) return damage * normalizedCriticalDamage;
        return damage;
    }

    /// <summary>
    /// Abstract method for triggering an attack. Player or AI will implement the actual input/trigger.
    /// </summary>
    public void PerformAttack()
    {
        if (!CanPerformAttack()) return;
        PerformAttackInternal();
        RaiseOnAttackPerformedEvent();
    }


    private bool CanPerformAttack()
    {
        return this.animationComponent.CanTransitionToAnimation(EquippedWeaponData.PrimaryAttackAnimationHash);
    }


    protected abstract void PerformAttackInternal();

    protected void RaiseOnAttackPerformedEvent()
    {
        OnAttackPerformed?.Invoke();
    }
}
