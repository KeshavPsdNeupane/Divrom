

public class EnemyAttackComponent : AttackComponentBase
{
    protected override void PerformAttackInternal()
    {
        float damage = CalculateDamage();
        Logger.Log($"Enemy Attack performed! Damage: {damage}, Base attack: {attack}");
    }
}
