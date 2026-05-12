using System;
using Kope.Core.Type.EnumAsset;
using UnityEngine;

namespace Kope.Component.Attack {
	[Serializable]
	public class WeaponData1 {
		public string WeaponName { get; private set; }
		public float AttackSpeed { get; private set; }
		public EnumInstance WeaponTypeInstance { get; private set; }
		public string PrimaryAttackAnimation { get; private set; }
		public int PrimaryAttackAnimationHash { get; private set; }
		public WeaponData1(string weaponName, EnumInstance weaponType, float attackSpeed,
		 string overrideAnimation) {
			this.WeaponName = weaponName;
			this.WeaponTypeInstance = weaponType;
			this.AttackSpeed = attackSpeed;
			this.PrimaryAttackAnimation = overrideAnimation;
			this.PrimaryAttackAnimationHash = Animator.StringToHash(PrimaryAttackAnimation);
		}
	}
}