using UnityEngine;

public class MovementComponent : MonoBehaviour
{
    public Rigidbody2D rb;
    [HideInInspector] public Vector2 direction;
    [HideInInspector] public Vector2 lastDirection;
    public float movementSpeed = 2.0f;

    public virtual void ApplyMovement(float movementSpeedMult = 1.0f)
    {
        rb.linearVelocity = direction.normalized * movementSpeed * movementSpeedMult;
        if(this.direction.sqrMagnitude > 0.01)
        {
            this.lastDirection = this.direction;
        }
    }

}
