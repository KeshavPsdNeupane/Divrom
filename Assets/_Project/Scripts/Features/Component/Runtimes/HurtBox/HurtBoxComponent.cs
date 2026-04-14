using System;
using System.Collections.Generic;
using Kope.Component.Combat.Interface;
using Kope.Component.HurtBox.Interface;
using Kope.Core.Init;
using UnityEngine;

namespace Kope.Component.HurtBox {
	public class HurtBoxComponent : InitializableBase, IHurtBoxComponent {
		[SerializeField] private Collider hurtBoxCollider;
		[SerializeField] private bool isInvulnerable;

		public event Action<HurtBoxHitInfo> OnHitEntity;

		public Collider HurtBoxCollider => hurtBoxCollider;

		protected override bool OnInit() {
			if (this.hurtBoxCollider == null) {
				Debug.LogError($"HurtBoxComponent on {gameObject.name} has no Collider assigned." +
				GetParentGameObjectHeirarchyMessage());
				return false;
			}
			return true;
		}

		public void HitEntity(
			GameObject caster,
			CombatType combatType = CombatType.Entity,
			EffectContext effectContext = default,
			IReadOnlyList<IEffectFactory<ICombatable>> effects = null) {
			if (this.isInvulnerable || caster == null) return;
			this.OnHitEntity?.Invoke(new HurtBoxHitInfo(this, caster, combatType, effectContext, effects));
		}
	}
}




