using System;
using Kope.Character.Stats;
using Kope.Component.Combat;
using Kope.Component.Combat.Interface;
using ThirdParty;
using UnityEngine;

namespace Kope.AbilitySystem.Effect {
	[Serializable]
	public struct DOTEffectData {
		[Header("Timing")]
		public float Duration;
		public float TickInterval;

		[Header("Scaling & Combat")]
		[Tooltip("If the caster attack component is null, this pity damage is dealt per tick")]
		[Min(1f)] public float PityDamagePerTick;
		[Min(0.01f)] public float DamageMultiplier;
		public DamageType DamageType;
		public CharacterStatType ScalingStat;
		[Min(0f)] public float PierceRatio;
		[Min(0f)] public float IgnoreResistance;
	}
	[Serializable]
	public struct DOTEffectLevelScaling {
		[Min(0f)] public int abilityUsedThreshold;
		[Min(0f)] public float duration;
		[Min(0f)] public float tickInterval;
		[Min(0f)] public float Multiplier;
		[Min(0f)] public float pierceRatio;
		[Min(0f)] public float ignoreResistance;
	}

	[Serializable]
	public class DamageOverTimeEffectFactory : IEffectFactory<ICombatable> {
		public DOTEffectData BaseData;
		[Tooltip("Level scaling data for the damage over time effect" +
		" Overrides base data when the ability use count meets a threshold. Must be in ascending order by abilityUsedThreshold.")]
		public DOTEffectLevelScaling[] LevelScaling = new DOTEffectLevelScaling[3];
		private DOTEffectData _cachedData;
		private int _nextRecomputeThreshold = 0;

		public IEffect<ICombatable> Create(EffectContext context = default) {
			// The lookup only advances a few times per ability lifetime, so caching avoids rescanning the array on every create.
			if (this._nextRecomputeThreshold < int.MaxValue
			&& context.AbilityUsedCount >= this._nextRecomputeThreshold) {
				this._cachedData = ResolveData(context.AbilityUsedCount, out this._nextRecomputeThreshold);
			}
			return new DamageOverTimeEffect(context, this._cachedData);
		}
		private DOTEffectData ResolveData(int useCount, out int newLevelThreshold) {
			if (this.LevelScaling == null || this.LevelScaling.Length == 0) {
				newLevelThreshold = int.MaxValue;
				return this.BaseData;
			}

			newLevelThreshold = this.LevelScaling[0].abilityUsedThreshold;
			for (int i = this.LevelScaling.Length - 1; i >= 0; i--) {
				if (useCount >= this.LevelScaling[i].abilityUsedThreshold) {
					newLevelThreshold = (i + 1 < this.LevelScaling.Length)
						? this.LevelScaling[i + 1].abilityUsedThreshold
						: int.MaxValue;

					// if the vars in the level scaling struct are set to 0 or less, 
					// it will fallback to the base data for that var, allowing for partial 
					// overrides instead of needing to specify all vars for each level
					return new DOTEffectData {
						Duration = this.LevelScaling[i].duration <= 0
						? this.BaseData.Duration : this.LevelScaling[i].duration,
						TickInterval = this.LevelScaling[i].tickInterval <= 0
						? this.BaseData.TickInterval : this.LevelScaling[i].tickInterval,
						PityDamagePerTick = this.BaseData.PityDamagePerTick,
						DamageMultiplier = this.LevelScaling[i].Multiplier <= 0
						? this.BaseData.DamageMultiplier : this.LevelScaling[i].Multiplier,
						DamageType = this.BaseData.DamageType,
						ScalingStat = this.BaseData.ScalingStat,
						PierceRatio = this.LevelScaling[i].pierceRatio <= 0
						? this.BaseData.PierceRatio : this.LevelScaling[i].pierceRatio,
						IgnoreResistance = this.LevelScaling[i].ignoreResistance <= 0
						? this.BaseData.IgnoreResistance : this.LevelScaling[i].ignoreResistance
					};
				}
			}
			return this.BaseData;

		}

		[Serializable]
		public class DamageOverTimeEffect : IEffect<ICombatable>, ITickableEffect {
			private readonly EffectContext _context;
			private readonly DOTEffectData _data;
			private readonly DamageDetail _calculatedTickDetail;

			public event Action<ITickableEffect> OnCompletedOrCancelled;

			private IntervalTimer _timer;
			private ICombatable _currentTarget;

			public DamageOverTimeEffect(EffectContext context, DOTEffectData data) {
				this._context = context;
				this._data = data;
				// pre calc here so we can just cache and dont do extra computation each tick, 
				// since the damage is not supposed to change over time for a single application of the effect
				// for the DamageEffect, calc on Apply is acceptable since DamageEffect is 1 hit wonder.
				float baseDmg = this._context.CasterAttack != null
					? this._context.CasterAttack.GetDamage(this._data.ScalingStat)
					: this._data.PityDamagePerTick;

				this._calculatedTickDetail = new DamageDetail(
					baseDmg * this._data.DamageMultiplier,
					this._context.Caster,
					this._data.DamageType,
					this._data.PierceRatio,
					this._data.IgnoreResistance,
					this._context.CasterLevel
				);
			}

			public float Apply(ICombatable target) {
				this._currentTarget = target;
				this._timer = new IntervalTimer(this._data.Duration, this._data.TickInterval) {
					OnInterval = OnInterval,
					OnTimerStop = OnStop
				};
				this._timer.Start();
				return 0f;
			}

			public void Tick(float deltaTime) => this._timer?.Tick(deltaTime);

			private void OnInterval() {
				// Target takes the pre-calculated hit each interval
				Debug.Log($"Applying DOT tick to {this._currentTarget}. Damage: {this._calculatedTickDetail.DamageAmount}");
				this._currentTarget?.TakeHit(this._calculatedTickDetail);
			}

			private void OnStop() => Cleanup();

			public void Cancel() {
				this._timer?.Stop();
				Cleanup();
			}

			private void Cleanup() {
				this._timer = null;
				this._currentTarget = null;
				this.OnCompletedOrCancelled?.Invoke(this);
			}
		}
	}
}