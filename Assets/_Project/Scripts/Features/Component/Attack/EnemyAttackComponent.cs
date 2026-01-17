

using Kope.Core.CompilerServices;

public class EnemyAttackComponent : AttackComponentBase
{
    protected override void PerformAttackInternal()
    {
        float damage = CalculateDamage();
        MyLogger.Log($"Enemy Attack performed! Damage: {damage}, Base attack: {attack}");
    }
}
