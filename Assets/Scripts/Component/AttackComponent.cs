using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

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

    }
    // Making this since we are using New Init Lifecycle 
    // that will 100% garuntee th CharacterStatsSystem is initialized before this component
    // so we can subscribe to the stats updates safely
    // leaving Start commented for reference
    // private void Start()
    // {
    //     // this func is also here to ensure subscription in case OnEnable is missed
    //     // and if we disable and enable the component later, we still
    //     // want to subscribe to the stats updates in OnEnable   
    //     Subscribe();
    // }

    void OnEnable()
    {
        // Checking So all the stats exist and are initialized
        // Checking still in just case for someone who doesn't use the Init Lifecycle
        if (this.statsSystem != null &&
            this.statsSystem.currentStats != null) Subscribe();
    }

    void OnDisable() => Unsubscribe();

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
        // Initial fetch
        AttackCallback(this.statsSystem.currentStats[CharacterStatType.ATK].GetValue());
        CriticalRateCallback(this.statsSystem.currentStats[CharacterStatType.CRATE].GetValue());
        CalculateDamage(this.statsSystem.currentStats[CharacterStatType.CDMG].GetValue());

    }

    private void Unsubscribe()
    {
        this.statsSystem.StatsUnsubscribe(CharacterStatType.ATK, AttackCallback);
        this.statsSystem.StatsUnsubscribe(CharacterStatType.CRATE, CriticalRateCallback);
        this.statsSystem.StatsUnsubscribe(CharacterStatType.CDMG, CalculateDamage);
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
        if (normalizedCriticalChance >= 1f) { return damage * this.normalizedCriticalDamage; }

        bool isCriticalHit = Random.value < this.normalizedCriticalChance;
        if (isCriticalHit)
        {
            return damage * this.normalizedCriticalDamage;
        }
        return damage;
    }

}
