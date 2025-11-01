using System.Collections.Generic;
using UnityEngine;



[System.Serializable]
public class CurrentStat
{
    [SerializeField] public PerkAdditiveStat perkAdditiveAndLevelingStat { get; set; }
    [SerializeField, HideInInspector] private List<L3BuffDebuffArmorStatusModifier> modifiers = new List<L3BuffDebuffArmorStatusModifier>();

    private float cachedValue;
    private bool isDirty = true;

    public CurrentStat(float baseLevelStat)
    {
        this.perkAdditiveAndLevelingStat = new PerkAdditiveStat(baseLevelStat);
    }

    public void OnEnable()
    {
        this.perkAdditiveAndLevelingStat.OnDirtyEventAction += MarkDirty;
        this.perkAdditiveAndLevelingStat.OnEnable();
    }

    public void OnDisable()
    {
        this.perkAdditiveAndLevelingStat.OnDirtyEventAction -= MarkDirty;
        this.perkAdditiveAndLevelingStat.OnDisable();
    }
    void MarkDirty()
    {
        this.isDirty = true;
    }

        private void CanRemoveModifier(L3BuffDebuffArmorStatusModifier csm)
    {
        csm.canRemove = true;
    }

    public void RemoveModifier(L3BuffDebuffArmorStatusModifier csm)
    {
        this.modifiers.Remove(csm);
        this.isDirty = true;
    }

    public void Update()
    {
        foreach (var mod in modifiers)
        {
            mod.durationCountDownTimer?.Tick();
        }
        for (int i = modifiers.Count - 1; i >= 0; i--)
        {
            var mod = modifiers[i];
            if (mod.canRemove)
            {
                RemoveModifier(mod);
            }
        }

    }
    public bool AddModifier(StatusEffect effect)
    {
       
        if (!effect.isDebuff)
        {
            return AddToList(effect);
        }

        bool isEnemy = effect.isDebuffFromEnemy;
        bool isArmor = effect.isDebuffFromArmor;
        bool isEnv = !isEnemy && !isArmor;

        if (isEnemy || isEnv)
        {  
            var existing = modifiers.Find(m => m.IsDebuff &&
                                               ((isEnemy && m.IsDebuffFromEnemy) ||
                                               (isEnv && !m.IsDebuffFromEnemy && !m.IsDebuffFromArmor)));
            if (existing != null)
            {
                if (effect.debuffPriority >= existing.DebuffPriority)
                    RemoveModifier(existing);
                else
                    return false; 
            }
        }
      return  AddToList(effect);
        
    }

    private bool AddToList(StatusEffect effect)
    {
        // Prevent  duplicate permanent buff modifiers from same source
        bool alreadyApplied = modifiers.Exists(
            m => m.Source == effect.source &&
            m.ModifierAmount == effect.modifierAmount &&
            m.IsPercentage == effect.isPercentage);

        if (alreadyApplied)
            return false;

        var mod = new L3BuffDebuffArmorStatusModifier(effect);

        // Subscribe to timer stop if temporary
        if (mod.Duration > 0)
        {
            mod.InitializeTimer();
            mod.durationCountDownTimer.OnTimerStop += () => CanRemoveModifier(mod);
            mod.StartTimer();
        }
        this.modifiers.Add(mod);
        this.isDirty = true;
        return true;
    }



    public void RemoveAllModifiersFromSource(string sourceName)
    {
        this.modifiers.RemoveAll(m => m.Source == sourceName);
        this.isDirty = true;
    }

    public void RemoveAllModifiers()
    {
        this.modifiers.Clear();
        this.isDirty = true;
    }

    public float GetValue()
    {
        if (!this.isDirty) return this.cachedValue;

        float baseValue = this.perkAdditiveAndLevelingStat.GetValue();
        float value = baseValue;
        foreach (var m in this.modifiers)
        {
            if (m.IsPercentage)
                value += (baseValue * (m.ModifierAmount * 0.01f));
            else
                value += m.ModifierAmount;
        }
        this.cachedValue = value;
        this.isDirty = false;
        return value;
    }

    public bool AddPerks(PerkStatData perkData)
    {
        this.perkAdditiveAndLevelingStat.AddModifier(perkData);
        this.isDirty = true;
        return true;
    }


    public void FacilateLevelup(int levelingStatAmount)
    {
        this.perkAdditiveAndLevelingStat.levelingStat.LevelUp(levelingStatAmount);
    }

}
  