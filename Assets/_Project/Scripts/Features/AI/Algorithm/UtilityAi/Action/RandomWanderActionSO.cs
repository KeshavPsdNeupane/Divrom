using Kope.AI.Utility;
using UnityEngine;
using Kope.Component.Movement;
using Kope.Core.EntityComponentSystem;


[CreateAssetMenu(fileName = "RandomWanderAction", menuName = "Scriptable Objects/AI/Utility/Actions/RandomWanderAction")]
public class RandomWanderActionSO : ActionSO
{
    [SerializeField] private float wanderRadius = 5f;

    [SerializeField] private int maxAttemptsToFindValidPoint = 10;

    private Vector2 targetPosition;
    private MovementComponentBase mc;

    protected override void OnInilialize(EntityComponentRegistry ctx)
    {
        if (!ctx.TryGetComponent(out MovementComponentBase newMc))
        {
            Debug.LogError("RandomWanderActionSO Initialization failed: MovementComponentBase not found.");
            return;
        }
        this.mc = newMc;
        this.targetPosition = GetRandomValidTarget();
        // Debug.Log($"RandomWanderActionSO initialized with MovementComponentBase on {ctx.EntityTransform.gameObject.name}"
        // + "Target Position: " + this.targetPosition);
    }

    protected override void OnEndOrAbort(EntityComponentRegistry ctx)
    {
        if (this.mc != null)
        {
            this.mc.StopMovement();
            this.mc = null;
        }
    }


    public override void TickUpdate(Context ctx)
    {
        return; // no need to proceed if we are not moving. since this action is purely movement based. 
        // so all the logic is in fixed update.
    }

    public override void TickFixedUpdate(Context ctx)
    {
        if (this.mc == null) return; // no need to proceed. if movement component is missing.

        // this need to be done in fixed update since it is directly manipulating movement component which is used in physics calculations. doing this in regular update can cause jittery movement and inconsistent behavior due to variable frame rates. by using fixed update, we ensure that movement logic is applied consistently with the physics engine's timing, resulting in smoother and more reliable movement behavior for the AI entity.
        // so the main reason is to ensure consistent and smooth movement behavior that is in sync with the physics engine, which is crucial for an action that directly controls movement like this RandomWanderAction.

        Vector2 target = this.targetPosition;
        float mass = this.mc.Mass;

        // from 'while' to 'if' to avoid potential infinite loops in single frame.
        // since we are not using the coroutine, so only one update per frame is possible.
        // removed coroutine because it was causing issues with AI Brain stopping actions.
        if ((this.mc.Position - target).sqrMagnitude > MovementComponentBase.MOVEMENT_EPSILON)
        {
            Vector2 targetDirection = (target - this.mc.Position).normalized;
            // never cache any value from mc.Direction, as it is mutable.
            var currentDirection = this.mc.Direction;
            float turnSpeed = 5f / mass; // Adjust turn speed based on mass
            currentDirection = Vector2.Lerp(currentDirection, targetDirection, turnSpeed * Time.fixedDeltaTime);
            currentDirection.Normalize();
            this.mc.SetMovementIntent(new MovementIntent(currentDirection, MovementIntentType.Move));
            return;
        }
        this.mc.StopMovement();
        MarkCompleted();
    }



    /// <summary>
    /// Using until my NavMesh2d solution is ready.
    /// </summary>
    /// <returns></returns>
    private Vector2 GetRandomValidTarget()
    {
        int dummy = this.maxAttemptsToFindValidPoint;
        Vector2 target = Random.insideUnitCircle.normalized * wanderRadius + this.mc.Position;
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
