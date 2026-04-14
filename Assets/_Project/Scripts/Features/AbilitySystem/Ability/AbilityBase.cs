using System;
using System.Collections.Generic;
using Kope.Component.Combat.Interface;
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
	[SerializeReference, SubclassSelector] public List<IEffectFactory<ICombatable>> effects = new();

	public abstract void Execute(ICombatable target, EffectContext context);
	protected abstract void HandleCastVFX(ICombatable target);
	protected abstract void HandleRunningVFX(ICombatable target);
	protected abstract void HandleSFX(ICombatable target);

}