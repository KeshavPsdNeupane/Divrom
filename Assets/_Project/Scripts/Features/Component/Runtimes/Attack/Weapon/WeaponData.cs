using System;
using Kope.Core.Type.EnumAsset;
using UnityEngine;

namespace Kope.Component.Attack {
	[Serializable]
	public class WeaponData1 {
		public EnumInstance WeaponTypeInstance { get; private set; }
		public string WeaponName { get; private set; }
		public float AttackSpeed { get; private set; }

		public int AnimationID { get; private set; }
		public string PrimaryAttackAnimationName { get; private set; }
		public WeaponData1(string weaponName, EnumInstance weaponType, float attackSpeed,
 			int animationId, string animationName) {

			this.WeaponName = weaponName;
			this.WeaponTypeInstance = weaponType;
			this.AttackSpeed = attackSpeed;
			this.AnimationID = animationId;
			this.PrimaryAttackAnimationName = animationName;
		}
	}
}