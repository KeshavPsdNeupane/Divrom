using UnityEngine.Events;
namespace Kope.Character.Stats {
	/// <summary>
	/// An advanced stat that combines leveling, point-based, and effect-based modifications.
	/// It uses LevelingStat as the base, PointStats for additional flat modifiers,
	/// and ArmorAndBuffAndDeBuffEffectStat for dynamic effects.
	/// for stats that require complex modifications. 
	/// like HP, ATK, DEF, etc.
	/// </summary>
	[System.Serializable]
	public class AdvanceStat : IStat {
		private readonly LevelingStat levelingStat;
		private readonly PointStats pointStats;
		private readonly ArmorAndBuffAndDeBuffEffectStat currentStat;

		public event UnityAction<float> OnStatsModified;

		public AdvanceStat(float baseValue) {
			this.levelingStat = new LevelingStat(baseValue);
			this.pointStats = new PointStats();
			this.currentStat = new ArmorAndBuffAndDeBuffEffectStat();

			this.pointStats.SetBase(this.levelingStat);
			this.currentStat.SetBase(this.pointStats);
		}

		private void NotifyStatModified() => this.OnStatsModified?.Invoke(GetValue());

		public float GetValue() => this.currentStat.GetValue();

		public void OnEnable() {
			this.levelingStat.OnDirtyEventAction += this.pointStats.MarkDirty;
			this.pointStats.OnDirtyEventAction += this.currentStat.MarkDirty;
			this.currentStat.OnDirtyEventAction += NotifyStatModified;
		}

		public void OnDisable() {
			this.levelingStat.OnDirtyEventAction -= this.pointStats.MarkDirty;
			this.pointStats.OnDirtyEventAction -= this.currentStat.MarkDirty;
			this.currentStat.OnDirtyEventAction -= NotifyStatModified;

		}

		public void Update() {
			this.currentStat.Update();
		}

		public void SetBaseValue(float newValue) {
			this.levelingStat.SetBaseValue(newValue);
		}

		public void LevelUp(float increaseValue) {
			this.levelingStat.LevelUp(increaseValue);
		}
		public void AddPointStat(float value) {
			this.pointStats.AddPointModifier(value);
		}
		public void RemoveAllPointStat() {
			this.pointStats.RemoveAllPointModifiers();
		}
		public bool AddStatusEffect(AbstractBaseModifier effect) {
			return this.currentStat.AddModifier(effect);
		}
		public void RemoveAllModifiers() {
			this.currentStat.RemoveAllModifiers();
		}
	}
}