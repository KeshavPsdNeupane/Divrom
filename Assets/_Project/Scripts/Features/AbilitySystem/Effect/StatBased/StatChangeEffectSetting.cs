using System;
using Kope.AbilitySystem.Effect;
using Kope.Character.Stats;
using Kope.Component.Combat.Interface;
using Kope.Core.Attribute;
using Kope.Core.Attribute.DataStructure;
using UnityEngine;

public enum StatChangeEffectType {
	BasicStatChange = 0,
	ResistanceStatChange = 1,
}

[Serializable]
public class StatChangeEffectSetting : DynamicSelection<StatChangeEffectType, IEffectFactory<IStatSystem>> {
	[SerializeField]
	[BindToEnum(StatChangeEffectType.BasicStatChange, typeof(BasicStatChangeEffectFactory))]
	private BasicStatChangeEffectFactory basicStatChangeEffectFactory;

	[SerializeField]
	[BindToEnum(StatChangeEffectType.ResistanceStatChange, typeof(ResistanceStatChangeEffectFactory))]
	private ResistanceStatChangeEffectFactory resistanceStatChangeEffectFactory;

	public IEffectFactory<IStatSystem> GetFactory() {
		return GetSelected();
	}
}
