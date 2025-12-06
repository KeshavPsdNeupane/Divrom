using System;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class Stat
{
    public L1LevelingStat levelingStat;
    public L2PerkStat perkStat;
    public L3ArmorAndBuffAndDeBuffEffectStat currentStat;

    public event UnityAction<float> OnStatsModified;

    public Stat(float baseValue)
    {
        levelingStat = new L1LevelingStat(baseValue);
        perkStat = new L2PerkStat();
        currentStat = new L3ArmorAndBuffAndDeBuffEffectStat();

        perkStat.SetBase(levelingStat);
        currentStat.SetBase(perkStat);
    }

    private void NotifyStatModified()
    {    
        float newValue = GetValue();
        this.OnStatsModified?.Invoke(newValue);
    }


    public float GetValue()
    {
        return currentStat.GetValue();
    }

    public void OnEnable()
    {
        levelingStat.OnDirtyEventAction += this.perkStat.MarkDirty;
        perkStat.OnDirtyEventAction += currentStat.MarkDirty;
        currentStat.OnDirtyEventAction += this.NotifyStatModified;
    }

    public void OnDisable()
    {
        levelingStat.OnDirtyEventAction -= this.perkStat.MarkDirty;
        perkStat.OnDirtyEventAction -= this.currentStat.MarkDirty;
        currentStat.OnDirtyEventAction -= this.NotifyStatModified;

    }
}