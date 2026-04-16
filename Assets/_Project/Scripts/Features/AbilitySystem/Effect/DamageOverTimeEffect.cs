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
	public class DamageOverTimeEffectFactory : IEffectFactory<ICombatable> {
		public DOTEffectData Data;

		public IEffect<ICombatable> Create(EffectContext context = default) =>
			new DamageOverTimeEffect(context, this.Data);
	}

	[Serializable]
	public class DamageOverTimeEffect : IEffect<ICombatable>, ITickableEffect {
		private readonly EffectContext _context;
		private readonly DOTEffectData _data;
		private readonly DamageDetail _calculatedTickDetail;

		public event Action<ITickableEffect> OnCompletedOrCancell;

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
			this.OnCompletedOrCancell?.Invoke(this);
		}
	}
}