using System;
using Kope.Component.Combat.Interface;
using Kope.Core.Attribute;
using Kope.Core.Attribute.DataStructure;
using UnityEngine;

namespace Kope.AbilitySystem.Effect.Settings {

	public enum DamageEffectType {
		Instant = 0,
		OverTime = 1,
		Vampiric = 2,
	}


	[Serializable]
	public class DamageEffectSetting : DynamicSelection<DamageEffectType, IEffectFactory<IDamagable>> {
		[SerializeField]
		[BindToEnum(DamageEffectType.Instant, typeof(InstantDamageEffectFactory))]
		private InstantDamageEffectFactory instantDamage;

		[SerializeField]
		[BindToEnum(DamageEffectType.OverTime, typeof(DamageOverTimeEffectFactory))]
		private DamageOverTimeEffectFactory overTimeDamage;

		[SerializeField]
		[BindToEnum(DamageEffectType.Vampiric, typeof(VampiricDamageEffectFactory))]
		private VampiricDamageEffectFactory vampiricDamage;

		public IEffectFactory<IDamagable> GetFactory() => GetSelected();

	}
}
