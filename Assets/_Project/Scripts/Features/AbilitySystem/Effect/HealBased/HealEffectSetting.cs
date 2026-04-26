using System;
using Kope.Component.Combat.Interface;
using Kope.Component.Health.Interface;
using Kope.Core.Attribute;
using Kope.Core.Attribute.DataStructure;
using UnityEngine;

namespace Kope.AbilitySystem.Effect.Settings {

	public enum HealEffectType {
		Instant = 0,
		OverTime = 1
	}

	[Serializable]
	public class HealEffectSetting : DynamicSelection<HealEffectType, IEffectFactory<IHealable>> {
		[SerializeField]
		[BindToEnum(HealEffectType.Instant, typeof(InstantHealEffectFactory))]
		private InstantHealEffectFactory instantHeal;

		[SerializeField]
		[BindToEnum(HealEffectType.OverTime, typeof(HealOverTimeEffectFactory))]
		private HealOverTimeEffectFactory overTimeHeal;
		public IEffectFactory<IHealable> GetFactory() => GetSelected();

	}

}