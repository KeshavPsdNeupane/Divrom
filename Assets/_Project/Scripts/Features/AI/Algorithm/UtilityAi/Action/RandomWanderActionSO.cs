using System.Collections;
using Kope.AI.Utility;
using UnityEngine;

[CreateAssetMenu(fileName = "RandomWanderAction", menuName = "Scriptable Objects/AI/Utility/Actions/RandomWanderAction")]
public class RandomWanderActionSO : ActionSO
{
    [SerializeField] private float wanderRadius = 5f;
    public override void Initialize(EntityContext ctx)
    {
        // no op
    }
    public override IEnumerator Execute(EntityContext ctx)
    {
        if (!ctx.TryGetComponent(out MovementComponentBase mc))
        {
            Debug.LogError("RandomWanderActionSO: MovementComponentBase not found.");
            yield break;
        }

        Vector2 wanderPoint = mc.Position + Random.insideUnitCircle * wanderRadius;

        while ((mc.Position - wanderPoint).sqrMagnitude > MovementComponentBase.DIRECTION_THRESHOLD)
        {
            Vector2 direction = (wanderPoint - mc.Position).normalized;
            mc.SetDirection(direction);
            mc.ApplyMovement();
            yield return null;
        }

        // stop movement
        mc.SetDirection(Vector2.zero);
        mc.ApplyMovement(0f);

        MarkCompleted();
    }

}
