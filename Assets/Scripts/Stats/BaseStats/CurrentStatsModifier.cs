using UnityEngine;

[System.Serializable]
public class CurrentStatModifier
{
    [HideInInspector] public bool canRemove = false;
    [HideInInspector] public CountdownTimer durationCountDownTimer;

    [SerializeField] public StatusEffect statusEffect;


    public bool IsDebuff => this.statusEffect.isDebuff;
    public string EffectName => this.statusEffect.effectName;
    public string Source => this.statusEffect.source;
    public int ModifierAmount => this.statusEffect.modifierAmount;
    public bool IsPercentage => this.statusEffect.isPercentage;
    public bool IsDebuffFromArmor => this.statusEffect.isDebuffFromArmor;
    public bool IsDebuffFromEnemy => this.statusEffect.isDebuffFromEnemy;
    public int DebuffPriority => this.statusEffect.debuffPriority;
    public float Duration => this.statusEffect.totalDuration;
    public CharacterStatType StatType => this.statusEffect.statType;



    public CurrentStatModifier(StatusEffect effect)
    {
        this.statusEffect = effect;
        InitializeTimer();
    }

    public void InitializeTimer()
    {
        if (this.statusEffect == null) return;

        if (this.durationCountDownTimer == null)
            this.durationCountDownTimer = new CountdownTimer(statusEffect.totalDuration);
        else
            this.durationCountDownTimer.Reset(statusEffect.totalDuration);
    }

    public void StartTimer()
    {
        if (this.durationCountDownTimer != null)
            this.durationCountDownTimer.Start();
    }   
}
