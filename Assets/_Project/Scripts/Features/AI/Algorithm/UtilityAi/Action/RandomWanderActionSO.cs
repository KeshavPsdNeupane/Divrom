using System.Collections;
using Kope.AI.Utility;
using UnityEngine;
using Kope.Component.Movement;
using System;
[CreateAssetMenu(fileName = "RandomWanderAction", menuName = "Scriptable Objects/AI/Utility/Actions/RandomWanderAction")]
public class RandomWanderActionSO : ActionSO
{
    [SerializeField] private float wanderRadius = 5f;
    [SerializeField] private float idleTimeAfterReachingTarget = 1f; // Wait time after reaching target

    [SerializeField] private int maxAttemptsToFindValidPoint = 10;

    private MovementComponentBase mc;
    private Vector2 currentDirection;

    /// <summary>
    /// Always Initialize components here.
    /// And cache references for later use.
    /// </summary>
    /// <param name="ctx"></param>
    public override void Initialize(EntityContext ctx)
    {
        base.Initialize(ctx);
        // no need to do base.Initialize(ctx) as it is empty. and it is scriptable object.
        // not InitializableBase.
        if (!ctx.TryGetComponent(out MovementComponentBase mc))
        {
            Debug.LogError("RandomWanderActionSO: MovementComponentBase not found.");
            return;
        }
        this.mc = mc;
    }

    public override void EndOrAbort(EntityContext ctx)
    {

        if (this.mc != null)
        {
            this.mc.StopMovement();
        }
        base.EndOrAbort(ctx);
    }


    public override IEnumerator Execute(EntityContext ctx)
    {
        if (this.mc == null)
        {
            Debug.LogError("RandomWanderActionSO: MovementComponentBase is null. Did you forget to Initialize?");
            yield break;
        }
        Vector2 target = GetRandomValidTarget();

        // Debug.Log("New position to wander to: " + target);

        currentDirection = this.mc.Direction;
        Rigidbody2D rb = this.mc.Rigidbody;

        // Move to target
        while ((this.mc.Position - target).sqrMagnitude > MovementComponentBase.MOVEMENT_EPSILON)
        {
            Vector2 targetDirection = (target - this.mc.Position).normalized;

            float turnSpeed = 5f / rb.mass;
            currentDirection = Vector2.Lerp(currentDirection, targetDirection, turnSpeed * Time.fixedDeltaTime);
            currentDirection.Normalize();

            this.mc.SetMovementIntent(new MovementIntent(currentDirection, MovementIntentType.Move));
            yield return new WaitForFixedUpdate();
        }

        this.mc.StopMovement();
        yield return new WaitForSeconds(idleTimeAfterReachingTarget);

        MarkCompleted();
    }



    /// <summary>
    /// Using until my NavMesh2d solution is ready.
    /// </summary>
    /// <returns></returns>
    private Vector2 GetRandomValidTarget()
    {
        int dummy = this.maxAttemptsToFindValidPoint;
        Vector2 target = UnityEngine.Random.insideUnitCircle.normalized * wanderRadius + this.mc.Position;
        // Ensure at least 1 unit distance in either X or Y axis (or both)
        Vector2 offset = target - this.mc.Position;
        if (Mathf.Abs(offset.x) < 1f && Mathf.Abs(offset.y) < 1f)
        {
            // If both are less than 1, scale the larger component to 1
            if (Mathf.Abs(offset.x) >= Mathf.Abs(offset.y))
                offset.x = Mathf.Sign(offset.x) * 1f;
            else
                offset.y = Mathf.Sign(offset.y) * 1f;

            target = this.mc.Position + offset;
        }

        return target;
    }

    #region NavMesh2D Placeholder, Just in case I want to use it later. right now I am using simple random point generation.
    // private Vector2 GetWanderPoint()
    // {

    //     for (int attempt = 0; attempt < this.maxAttemptsToFindValidPoint; ++attempt)
    //     {
    //         Vector2 randomPoint = UnityEngine.Random.insideUnitCircle * wanderRadius + this.mc.Position;

    //         // Check if the point is valid (e.g., is it on the NavMesh or outside a wall?)
    //         if (IsValidPoint(randomPoint))
    //         {
    //             return randomPoint; // Only exit here if the point is good!
    //         }
    //     }

    //     // If we exhausted all attempts without finding a valid point
    //     return this.mc.Position;
    // }

    // // Example placeholder for your validation logic
    // private bool IsValidPoint(Vector2 point)
    // {
    //     // Add your obstacle/boundary detection here
    //     return true;
    // }
    #endregion


}
