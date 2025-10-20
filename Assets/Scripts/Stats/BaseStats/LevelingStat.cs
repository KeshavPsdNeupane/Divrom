using System;
using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public class BaseLevelingIncreasingStatSaveData
{
    public float baseValue;
}


public class LevelingStat
{
    [SerializeField] private float baseValue;
    private float cachedValue;
    private bool isDirty = true;

    public Action OnDirtyEventAction { get; set; }

    public LevelingStat(float baseValue)
    {
        this.baseValue = baseValue;
        this.cachedValue = baseValue;
    }

    public float GetValue()
    {
        if (!isDirty) return cachedValue;
        this.cachedValue = baseValue;
        this.isDirty = false;
        return cachedValue;
    }

    public void LevelUp(float increaseAmount)
    {
        this.baseValue += increaseAmount;
        MarkDirty();
    }

    private void MarkDirty()
    {
        this.isDirty = true;
        this.OnDirtyEventAction?.Invoke();
    }





    public BaseLevelingIncreasingStatSaveData GetSaveData() => new BaseLevelingIncreasingStatSaveData { baseValue = baseValue };
    public void LoadFromData(BaseLevelingIncreasingStatSaveData data) { baseValue = data.baseValue; MarkDirty(); }
}
