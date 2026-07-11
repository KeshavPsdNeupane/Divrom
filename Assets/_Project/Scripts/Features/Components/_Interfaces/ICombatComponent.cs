using System;
using Kope.Component.Attack;
using Kope.Component.Health.Interface;
using Kope.Component.HitBox.Interface;
using Kope.Component.Movement;
using Kope.Core;
using UnityEngine;

namespace Kope.Component.Combat.Interface {
	public struct EffectContext : IEquatable<EffectContext> {
		public AxisMode Dimension; // whether it is 2d or 3d, for proper physics calculations and effect application.
		public int CasterLevel;       // For Level Scaling math
		public GameObject Caster;     // For attribution (who killed who)
		public Vector3 HitPoint;      // For Knockback calculation
		public IAttackComponent CasterAttack; // For scaling damage (ATK/SP)
		public IMovementComponent CasterMovement; // For Knockback calculation
		public IHealable CasterHealth; // For "Vampire" or "Thorn" effects

		// this will be inited by the ability themself.
		public int AbilityUsedCount; // For "Next Level Scaling" effects
		private int hashcode;
		public readonly bool Equals(EffectContext other) {
			return this.Caster == other.Caster
				&& this.HitPoint == other.HitPoint
				&& this.CasterLevel == other.CasterLevel
				&& this.CasterAttack == other.CasterAttack
				&& this.CasterHealth == other.CasterHealth
				&& this.AbilityUsedCount == other.AbilityUsedCount;
		}
		public override readonly bool Equals(object obj) {
			return obj is EffectContext other && Equals(other);
		}
		public override int GetHashCode() {
			if (this.hashcode == 0) {
				this.hashcode = HashCode.Combine(
					this.Caster, this.HitPoint,
					this.CasterLevel, this.CasterAttack,
					this.CasterHealth, this.AbilityUsedCount
					);
			}
			return this.hashcode;
		}
		public static bool operator ==(EffectContext left, EffectContext right) {
			return left.Equals(right);
		}
		public static bool operator !=(EffectContext left, EffectContext right) {
			return !left.Equals(right);
		}
	}
	public interface IDamagable {
		IHitBoxComponent HurtBox { get; }
		float TakeHit(DamageDetail damageDetail);
		void TakeDamageDebugOnly(int amount);
	}
	public interface IEffectFactory<TTarget> : ISerializationCallbackReceiver {
		IEffect<TTarget> Create(EffectContext context = default);
	}


	public interface IEffect<TTarget> {
		void Apply(TTarget target);
	}

	public interface ITickableEffect {
		event Action<ITickableEffect> OnCompletedOrCancelled;
		void Tick(float deltaTime);
		void Cancel();
	}
}