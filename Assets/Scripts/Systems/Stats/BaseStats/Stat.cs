using System;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class Stat
{
    private readonly LevelingStat levelingStat;
    private readonly PointStats pointStats;
    private readonly PerkStat perkStat;
    private readonly ArmorAndBuffAndDeBuffEffectStat currentStat;


    // public getters
    public LevelingStat LevelingStat => this.levelingStat;
    public PointStats PointStats => this.pointStats;
    public PerkStat PerkStat => this.perkStat;
    public ArmorAndBuffAndDeBuffEffectStat CurrentStat => this.currentStat;

    public event UnityAction<float> OnStatsModified;

    public Stat(float baseValue)
    {
        this.levelingStat = new LevelingStat(baseValue);
        this.pointStats = new PointStats();
        this.perkStat = new PerkStat();
        this.currentStat = new ArmorAndBuffAndDeBuffEffectStat();

        this.pointStats.SetBase(this.levelingStat);
        this.perkStat.SetBase(this.pointStats);
        this.currentStat.SetBase(this.perkStat);
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
        this.levelingStat.OnDirtyEventAction += this.pointStats.MarkDirty;
        this.pointStats.OnDirtyEventAction += this.perkStat.MarkDirty;
        this.perkStat.OnDirtyEventAction += this.currentStat.MarkDirty;
        this.currentStat.OnDirtyEventAction += this.NotifyStatModified;
    }

    public void OnDisable()
    {
        this.levelingStat.OnDirtyEventAction -= this.pointStats.MarkDirty;
        this.pointStats.OnDirtyEventAction -= this.perkStat.MarkDirty;
        this.perkStat.OnDirtyEventAction -= this.currentStat.MarkDirty;
        this.currentStat.OnDirtyEventAction -= this.NotifyStatModified;

    }
}