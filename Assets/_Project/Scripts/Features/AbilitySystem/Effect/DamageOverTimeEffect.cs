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
		public float PityDamagePerTick;
		public float DamageMultiplier;
		public DamageType DamageType;
		public CharacterStatType ScalingStat;
		public float PierceRatio;
		public float IgnoreResistance;
	}
	[Serializable]
	public struct DOTEffectLevelScaling {
		public int abilityUsedThreshold;
		public float duration;
		public float tickInterval;
		public float Multiplier;
		public float pierceRatio;
		public float ignoreResistance;
	}

	[Serializable]
	public class DamageOverTimeEffectFactory : IEffectFactory<ICombatable> {
		public DOTEffectData BaseData;
		[Tooltip("Level scaling data for the damage over time effect" +
		"Will override base data at specified usage thresholds")]
		public DOTEffectLevelScaling[] LevelScaling = new DOTEffectLevelScaling[3];
		private DOTEffectData _cachedData;
		private int _cachedNewLevelThreshold = 0;

		public IEffect<ICombatable> Create(EffectContext context = default) {
			if (context.AbilityUsedCount >= this._cachedNewLevelThreshold) {
				this._cachedData = ResolveData(context.AbilityUsedCount, out this._cachedNewLevelThreshold);
			}
			return new DamageOverTimeEffect(context, this._cachedData);
		}
		private DOTEffectData ResolveData(int useCount, out int newLevelThreshold) {
			newLevelThreshold = 0;
			for (int i = this.LevelScaling.Length - 1; i >= 0; i--) {
				if (useCount >= this.LevelScaling[i].abilityUsedThreshold) {
					newLevelThreshold = this.LevelScaling[i].abilityUsedThreshold;
					return new DOTEffectData {
						Duration = this.LevelScaling[i].duration,
						TickInterval = this.LevelScaling[i].tickInterval,
						PityDamagePerTick = this.BaseData.PityDamagePerTick,
						DamageMultiplier = this.LevelScaling[i].Multiplier,
						DamageType = this.BaseData.DamageType,
						ScalingStat = this.BaseData.ScalingStat,
						PierceRatio = this.LevelScaling[i].pierceRatio,
						IgnoreResistance = this.LevelScaling[i].ignoreResistance
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