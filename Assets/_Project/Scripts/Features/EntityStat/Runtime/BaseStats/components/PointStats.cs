using System;
using System.Collections.Generic;

namespace Kope.Character.Stats
{
    public class PointStats : IBaseStatProvider
    {
        private IBaseStatProvider baseStat;
        private readonly List<float> modifiers = new();
        private bool isDirty = true;
        private float cachedValue;

        public event Action OnDirtyEventAction;

        public void SetBase(IBaseStatProvider baseStat) => this.baseStat = baseStat;


        public void AddPointModifier(float modifier)
        {
            this.modifiers.Add(modifier);
            MarkDirty();
        }

        public void MarkDirty()
        {
            this.isDirty = true;
            this.OnDirtyEventAction?.Invoke();
        }

        public float GetValue()
        {
            if (!this.isDirty) return this.cachedValue;
            float baseStatValue = this.baseStat?.GetValue() ?? 0f;
            float value = baseStatValue;
            foreach (var mod in this.modifiers)
            {
                value += mod;
            }
            this.cachedValue = value;
            this.isDirty = false;
            return this.cachedValue;
        }
        public void RemoveAllPointModifiers()
        {
            this.modifiers.Clear();
            MarkDirty();
        }
    }
}