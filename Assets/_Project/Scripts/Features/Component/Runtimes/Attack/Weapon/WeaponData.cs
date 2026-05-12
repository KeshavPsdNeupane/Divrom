using System;
using UnityEngine;

namespace Kope.Component.Attack {
	[Serializable]
	public class WeaponData1 {
		private readonly string weaponName;
		private readonly string weaponTypeName;
		private readonly int weaponTypeID;
		private readonly float attackSpeed;
		private readonly string primaryAttackAnimation;
		private readonly int primaryAttackAnimationHash;


		public string WeaponName => weaponName;
		public string WeaponType => weaponTypeName;
		public int WeaponTypeID => weaponTypeID;
		public string PrimaryAttackAnimation => primaryAttackAnimation;
		public float AttackSpeed => attackSpeed;
		public int PrimaryAttackAnimationHash => primaryAttackAnimationHash;

		public WeaponData1(string weaponName, string weaponType, int weaponTypeID, float attackSpeed,
		 string overrideAnimation) {
			this.weaponName = weaponName;
			this.weaponTypeName = weaponType;
			this.weaponTypeID = weaponTypeID;
			this.attackSpeed = attackSpeed;
			this.primaryAttackAnimation = overrideAnimation;
			this.primaryAttackAnimationHash = Animator.StringToHash(primaryAttackAnimation.ToString());
		}
	}
}