using System;
using Kope.Character.Stats;
using Kope.Component.Combat;
using Kope.Component.Combat.Interface;
using ThirdParty;
using UnityEngine;

namespace Kope.AbilitySystem.Effect {

	[Serializable]
	public class DamageOverTimeEffectFactory : IEffectFactory<IDamagable> {
		[SerializeField] private DOTEffectData BaseData;
		[Tooltip("Level scaling data for the damage over time effect" +
		" Overrides base data when the ability use count meets a threshold. Must be in ascending order by abilityUsedThreshold.")]
		[SerializeField] private DOTEffectLevelScaling[] nextLevelScaling = new DOTEffectLevelScaling[3];
		private DOTEffectData _cachedData;
		private int _nextRecomputeThreshold = 0;

		public IEffect<IDamagable> Create(EffectContext context = default) {
			// The lookup only advances a few times per ability lifetime, so caching avoids rescanning the array on every create.
			if (this._nextRecomputeThreshold < int.MaxValue
			&& context.AbilityUsedCount >= this._nextRecomputeThreshold) {
				this._cachedData = ResolveData(context.AbilityUsedCount, out this._nextRecomputeThreshold);
			}
			return new DamageOverTimeEffect(context, this._cachedData);
		}


		private DOTEffectData ResolveData(int useCount, out int newLevelThreshold) {
			if (this.nextLevelScaling == null || this.nextLevelScaling.Length == 0) {
				newLevelThreshold = int.MaxValue;
				return this.BaseData;
			}

			newLevelThreshold = this.nextLevelScaling[0].AbilityUsedThreshold;
			for (int i = this.nextLevelScaling.Length - 1; i >= 0; i--) {
				if (useCount >= this.nextLevelScaling[i].AbilityUsedThreshold) {
					newLevelThreshold = (i + 1 < this.nextLevelScaling.Length)
						? this.nextLevelScaling[i + 1].AbilityUsedThreshold
						: int.MaxValue;

					// if the vars in the level scaling struct are set to 0 or less, 
					// it will fallback to the base data for that var, allowing for partial 
					// overrides instead of needing to specify all vars for each level
					return new DOTEffectData {
						Duration = this.nextLevelScaling[i].Duration <= 0
						? this.BaseData.Duration : this.nextLevelScaling[i].Duration,
						TickInterval = this.nextLevelScaling[i].TickInterval <= 0
						? this.BaseData.TickInterval : this.nextLevelScaling[i].TickInterval,
						PityDamagePerTick = this.BaseData.PityDamagePerTick,
						DamageMultiplier = this.nextLevelScaling[i].Multiplier <= 0
						? this.BaseData.DamageMultiplier : this.nextLevelScaling[i].Multiplier,
						DamageType = this.BaseData.DamageType,
						ScalingStat = this.BaseData.ScalingStat,
						PierceRatio = this.nextLevelScaling[i].PierceRatio <= 0
						? this.BaseData.PierceRatio : this.nextLevelScaling[i].PierceRatio,
						IgnoreResistance = this.nextLevelScaling[i].IgnoreResistance <= 0
						? this.BaseData.IgnoreResistance : this.nextLevelScaling[i].IgnoreResistance
					};
				}
			}
			return this.BaseData;

		}
		public void OnBeforeSerialize() { }

		public void OnAfterDeserialize() {
			if (this.nextLevelScaling == null || this.nextLevelScaling.Length == 0) {
				this.nextLevelScaling = new DOTEffectLevelScaling[3];
			}
		}

	}
	[Serializable]
	public class DamageOverTimeEffect : IEffect<IDamagable>, ITickableEffect {
		private readonly EffectContext _context;
		private readonly DOTEffectData _data;
		private readonly DamageDetail _calculatedTickDetail;

		public event Action<ITickableEffect> OnCompletedOrCancelled;

		private IntervalTimer _timer;
		private IDamagable _currentTarget;

		public DamageOverTimeEffect(EffectContext context, DOTEffectData data) {
			this._context = context;
			this._data = data;

			float finalDamage = this._context.CasterAttack != null
				? this._context.CasterAttack.GetDamageValue(
					this._data.ScalingStat, this._data.DamageMultiplier
					) : this._data.PityDamagePerTick * this._data.DamageMultiplier;



			this._calculatedTickDetail = new DamageDetail(
				finalDamage,
				this._context.Caster,
				this._data.DamageType,
				this._data.PierceRatio,
				this._data.IgnoreResistance,
				this._context.CasterLevel
			);
		}

		public void Apply(IDamagable target) {
			this._currentTarget = target;
			this._timer = new IntervalTimer(this._data.Duration, this._data.TickInterval) {
				OnInterval = OnInterval,
				OnTimerStop = OnStop
			};
			this._timer.Start();
			//first hit.
			_ = this._currentTarget.TakeHit(this._calculatedTickDetail);
		}

		public void Tick(float deltaTime) => this._timer?.Tick(deltaTime);

		private void OnInterval() {
			_ = this._currentTarget?.TakeHit(this._calculatedTickDetail);
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
		[Min(0f)] public int AbilityUsedThreshold;
		[Min(0f)] public float Duration;
		[Min(0f)] public float TickInterval;
		[Min(0f)] public float Multiplier;
		[Min(0f)] public float PierceRatio;
		[Min(0f)] public float IgnoreResistance;
	}

}