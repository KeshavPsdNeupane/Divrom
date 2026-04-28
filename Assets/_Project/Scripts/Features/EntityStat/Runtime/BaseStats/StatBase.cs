using UnityEngine.Events;

namespace Kope.Character.Stats {
	/// <summary>
	/// Basic stat class that holds a base value and allows for modifications through armor, buffs, and debuffs.
	/// Used for stats like resistances that do not level up.
	/// </summary>
	public class StatBase : IStat {
		private readonly LevelingStat levelingStat;
		private readonly ArmorAndBuffAndDeBuffEffectStat currentStat;

		public event UnityAction<float> OnStatsModified;

		public StatBase(float baseValue) {
			this.levelingStat = new LevelingStat(baseValue);
			this.currentStat = new ArmorAndBuffAndDeBuffEffectStat();

			this.currentStat.SetBase(this.levelingStat);
		}

		private void NotifyStatModified() => this.OnStatsModified?.Invoke(GetValue());

		public float GetValue() => this.currentStat.GetValue();

		public void OnEnable() {
			this.levelingStat.OnDirtyEventAction += this.currentStat.MarkDirty;
			this.currentStat.OnDirtyEventAction += NotifyStatModified;
		}


		public void OnDisable() {
			this.levelingStat.OnDirtyEventAction -= this.currentStat.MarkDirty;
			this.currentStat.OnDirtyEventAction -= NotifyStatModified;
		}
		public void Update() {
			this.currentStat.Update();
		}
		public bool AddModifier(AbstractBaseModifier modifier) {
			return this.currentStat.AddModifier(modifier);
		}
		public void RemoveAllModifiers() {
			this.currentStat.RemoveAllModifiers();
		}
	}
}
