
using UnityEngine;
namespace Kope.Component.Attack {

	public class EnemyAttackComponent : AttackComponentBase {
		protected override float PerformAttackInternal() {
			float damage = GetDamageValue();
			Debug.Log($"Enemy Attack performed! Damage: {damage}");
			return damage;
		}
	}
}