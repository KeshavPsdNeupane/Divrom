using System;
using System.Collections.Generic;
using Kope.Component.HurtBox.Interface;
using Kope.Core.Attributes;
using UnityEngine;

[Serializable]
public abstract class AbilityBase : ScriptableObject {
	[SerializeField, Tooltip("Audio clip to play when the ability is cast.")]
	protected AudioClip castSfx;
	[SerializeField, Tooltip("Visual effect to play when the ability is cast.")]
	protected GameObject castVfx;
	[SerializeField, Tooltip("Visual effect to play while the ability is active.")]
	protected GameObject runningVfx;

	[Header("Effects")]
	[SerializeReference, SubclassSelector] public List<IEffectFactory<IDamageable>> effects = new();

	public abstract void Execute(IDamageable target, EffectContext context);
	protected abstract void HandleCastVFX(IDamageable target);
	protected abstract void HandleRunningVFX(IDamageable target);
	protected abstract void HandleSFX(IDamageable target);

}