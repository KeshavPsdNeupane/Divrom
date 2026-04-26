using System;
using Kope.Component.Ability.Targeting;
using Kope.Core.Attribute;
using Kope.Core.Attribute.DataStructure;
using UnityEngine;


public enum TargetingType {
	// We explicitly set this to 0 as the 'Safe Default'.
	// If a designer adds this component and forgets to configure it, 
	// the ability will simply target the caster rather than failing.
	SelfTargeting = 0,

	AOETargeting = 1,
	ProjectileBasedTargeting = 2,
}
[Serializable]
public class TargetingSettings : DynamicSelection<TargetingType, ITargetingFactory> {
	[SerializeField]
	[BindToEnum(TargetingType.AOETargeting, typeof(AreaTargetingStrategyFactory))]
	private AreaTargetingStrategyFactory area;

	[SerializeField]
	[BindToEnum(TargetingType.ProjectileBasedTargeting, typeof(ProjectileTargetingStrategyFactory))]
	private ProjectileTargetingStrategyFactory projectile;

	[SerializeField]
	[BindToEnum(TargetingType.SelfTargeting, typeof(SelfTargetingStrategyFactory))]
	private SelfTargetingStrategyFactory self;

	public ITargetingFactory GetFactory() => GetSelected();
}