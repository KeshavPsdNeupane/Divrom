using System;
using UnityEngine;

[System.Serializable]
public class Stat
{
    public L1LevelingStat levelingStat;
    public L2PerkStat perkStat;
    public L3ArmorAndBuffAndDeBuffEffectStat currentStat;

    public Stat(float baseValue)
    {
        levelingStat = new L1LevelingStat(baseValue);
        perkStat = new L2PerkStat();
        currentStat = new L3ArmorAndBuffAndDeBuffEffectStat();

        perkStat.SetBase(levelingStat);
        currentStat.SetBase(perkStat);


    }

    public float GetValue()
    {
        return currentStat.GetValue();
    }

    public void OnEnable()
    {
        levelingStat.OnDirtyEventAction += this.perkStat.MarkDirty;
        perkStat.OnDirtyEventAction += currentStat.MarkDirty;
    }

    public void OnDisable()
    {
        levelingStat.OnDirtyEventAction -= this.perkStat.MarkDirty;
        perkStat.OnDirtyEventAction -= this.currentStat.MarkDirty;

    }
}