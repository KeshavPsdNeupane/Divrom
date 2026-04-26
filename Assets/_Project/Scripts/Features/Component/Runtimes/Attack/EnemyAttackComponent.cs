using Kope.Core.CompilerServices;
namespace Kope.Component.Attack {

	public class EnemyAttackComponent : AttackComponentBase {
		protected override float PerformAttackInternal() {
			float damage = GetDamageValue();
			MyLogger.Log($"Enemy Attack performed! Damage: {damage}");
			return damage;
		}
	}
}