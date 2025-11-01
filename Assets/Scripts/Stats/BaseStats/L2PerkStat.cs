using System;
using System.Collections.Generic;
using UnityEngine;

public struct PerkStatData
{
    public string perkName;
    public float perkStatModifier;
    public bool isPercentage;
    public PerkStatData(string statName, float modifier,bool isPercentage)
    {
        this.perkName = statName;
        this.perkStatModifier = modifier;
        this.isPercentage = isPercentage;
    }
}


public class L2PerkStat : IBaseStatProvider
{
    private IBaseStatProvider baseStat;
    [SerializeField, HideInInspector] private List<PerkStatData> modifiers = new();

    private bool isDirty = true;
    private float cachedValue;

    public event Action OnDirtyEventAction;

    public void SetBase(IBaseStatProvider baseStat) => this.baseStat = baseStat;

    public void MarkDirty()
    {
        this.isDirty = true;
        this.OnDirtyEventAction?.Invoke();
    }

    public void AddPerk(PerkStatData mod)
    {
        this.modifiers.Add(mod);
        MarkDirty();
    }

    public void RemoveAllPerks()
    {
        this.modifiers.Clear();
        MarkDirty();
    }


    public float GetValue()
    {
        if (!this.isDirty) return this.cachedValue;

        float baseValue = this.baseStat?.GetValue() ?? 0f;
        float value = baseValue;

        foreach (var m in this.modifiers)
        {
            if (m.isPercentage)
                value += baseValue * (m.perkStatModifier * 0.01f);
            else
                value += m.perkStatModifier;
        }

        this.cachedValue = value;
        this.isDirty = false;
        return value;
    }
}
