using System;
using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public class BaseLevelingIncreasingStatSaveData
{
    public int baseValue;
}


public class LevelingStat
{
    [SerializeField] private int baseValue;
    private int cachedValue;
    private bool isDirty = true;

    public Action OnDirtyEventAction { get; set; }

    public LevelingStat(int baseValue)
    {
        this.baseValue = baseValue;
        this.cachedValue = baseValue;
    }

    public int GetValue()
    {
        if (!isDirty) return cachedValue;
        this.cachedValue = baseValue;
        this.isDirty = false;
        return cachedValue;
    }

    public void LevelUp(int increaseAmount)
    {
        this.baseValue += increaseAmount;
        MarkDirty();
    }

    private void MarkDirty()
    {
        this.isDirty = true;
        this.OnDirtyEventAction?.Invoke();
    }

    // Save & Load
    public BaseLevelingIncreasingStatSaveData GetSaveData() => new BaseLevelingIncreasingStatSaveData { baseValue = baseValue };
    public void LoadFromData(BaseLevelingIncreasingStatSaveData data) { baseValue = data.baseValue; MarkDirty(); }
}
