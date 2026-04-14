using System;
using UnityEngine;

namespace Kope.Component.HurtBox {
	public class HurtBoxComponent : MonoBehaviour {
		[SerializeField] private Collider hurtBoxCollider;
		[SerializeField] private bool isInvulnerable;
		[SerializeField] private bool isCombatTypeEntity = true;

		public Collider HurtBoxCollider => hurtBoxCollider;


	}
}




