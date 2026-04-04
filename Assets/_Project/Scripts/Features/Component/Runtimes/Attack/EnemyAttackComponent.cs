using Kope.Core.CompilerServices;
namespace Kope.Component.Attack {

	public class EnemyAttackComponent : AttackComponentBase {
		protected override float PerformAttackInternal() {
			float damage = CalculateDamage();
			MyLogger.Log($"Enemy Attack performed! Damage: {damage}, Base attack: {_attack}");
			return damage;
		}
	}
}