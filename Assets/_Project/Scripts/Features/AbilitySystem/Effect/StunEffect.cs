using System;
using Kope.Component.Combat.Interface;
using Kope.Component.Movement;
using UnityEngine;

namespace Kope.AbilitySystem.Effect {
	[Serializable]
	public class StunEffectFactory : IEffectFactory<IStunnable> {
		[Min(0f)] public float duration = 1f;
		public bool superStun;

		public IEffect<IStunnable> Create(EffectContext context = default) {
			return new StunEffect(duration, superStun);
		}
	}

	[Serializable]
	public class StunEffect : IEffect<IStunnable> {
		private readonly float duration;
		private readonly bool superStun;

		public StunEffect(float duration, bool superStun) {
			this.duration = duration;
			this.superStun = superStun;
		}

		public float Apply(IStunnable target) {
			if (target == null) return 0f;

			if (this.superStun) {
				target.SuperStun(this.duration);
			} else {
				target.Stun(this.duration);
			}

			return 0f;
		}
	}
}