using System;
using UnityEngine;

public class L1LevelingStat : IBaseStatProvider
{
    [SerializeField] private float baseValue;
    private bool isDirty = true;
    private float cachedValue;

    public event Action OnDirtyEventAction;

    public L1LevelingStat(float baseValue)
    {
        this.baseValue = baseValue;
        this.cachedValue = baseValue;
    }

    public void LevelUp(float increaseAmount)
    {
        this.baseValue += increaseAmount;
        MarkDirty();
    }

    public void MarkDirty()
    {
       this. isDirty = true;
       this. OnDirtyEventAction?.Invoke();
    }

    public float GetValue()
    {
        if (!this.isDirty) return this.cachedValue;
        this.cachedValue =this. baseValue;
        this.isDirty = false;
        return this.cachedValue;
    }
}
