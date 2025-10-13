using UnityEngine;
using System.Collections.Generic;
using System;


public struct PerkStatData
{
    public string perkName;
    public int perkStatModifier;
    public bool isPercentage;
    public PerkStatData(string statName, int modifier,bool isPercentage)
    {
        this.perkName = statName;
        this.perkStatModifier = modifier;
        this.isPercentage = isPercentage;
    }
}


[System.Serializable]
public class PerkAdditiveStat
{
    [SerializeField] public LevelingStat levelingStat { get; private set; }
    [SerializeField, HideInInspector] private List<PerkStatData> modifiers;

    private int cachedValue;
    private bool isDirty = true;

    public Action OnDirtyEventAction { get; set; }

    public PerkAdditiveStat(int baseLevelStat)
    {
      this.  levelingStat = new LevelingStat(baseLevelStat);
        this.modifiers = new List<PerkStatData>();
    }

    public void OnEnable() => this.levelingStat.OnDirtyEventAction += MarkDirty;
    public void OnDisable() => this.levelingStat.OnDirtyEventAction -= MarkDirty;

    private void MarkDirty()
    {
        this.isDirty = true;
        this.OnDirtyEventAction?.Invoke();
    }

    public void AddModifier(PerkStatData mod)
    {
        this.modifiers.Add(mod);
        MarkDirty();
    }

    public void RemoveAllModifiers()
    {
        this.modifiers.Clear();
        MarkDirty();
    }

    public int GetValue()
    {
        if (!this.isDirty) return this.cachedValue;

        int baseValue = levelingStat.GetValue();
        int value = levelingStat.GetValue();
        foreach (var m in modifiers)
        {
            if (m.isPercentage)
                value += (int)(baseValue * (m.perkStatModifier * 0.01));
            else
                value += m.perkStatModifier;
        }

        this.cachedValue = value;
        this.isDirty = false;
        return value;
    }
}