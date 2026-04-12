using System;
using System.Collections.Generic;
using Kope.Component.HurtBox.Interface;
using Kope.Core.Attributes;
using UnityEngine;

[Serializable]
public abstract class AbilityBase : ScriptableObject {
	public AudioClip castSfx;
	public GameObject castVfx;
	public GameObject runningVfx;

	[Header("Effects")]
	[SerializeReference, SubclassSelector] public List<IEffectFactory<IDamageable>> effects = new();

	public void Execute(IDamageable target, EffectContext context) {
		HandleVFX(target);
		foreach (var factory in effects) {
			target.ApplyEffect(factory.Create(context));
		}
	}

	void HandleVFX(IDamageable target) {
		// no op
	}
}