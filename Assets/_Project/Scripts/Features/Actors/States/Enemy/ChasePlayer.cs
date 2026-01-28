using UnityEngine;
using Kope.Component.Movement;

[RequireComponent(typeof(CircleCollider2D))]
public class ChasePlayer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CircleCollider2D chaseRadius;
    [SerializeField] private MovementComponentBase movementComponent;
    [SerializeField] private float chaseRadiusValue = 3f;

    [Header("Chase Settings")]
    [Tooltip("How smoothly the enemy turns toward the player. Lower = slower turn.")]
    [Range(0f, 1f)]
    [SerializeField] private float chaseSmoothing = 0.15f;

    private Transform playerTransform;

    private void Awake()
    {
        if (this.chaseRadius == null)
            this.chaseRadius = GetComponent<CircleCollider2D>();

        if (this.movementComponent == null)
            this.movementComponent = GetComponent<MovementComponentBase>();

        this.chaseRadius.isTrigger = true;
        this.chaseRadius.radius = chaseRadiusValue;
    }

    private void Update()
    {
        if (this.playerTransform == null)
        {
            this.movementComponent.StopMovement();
            return;
        }

        Vector2 targetDirection = (this.playerTransform.position - transform.position).normalized;
        this.movementComponent.SetMovementIntent(new MovementIntent
        (Vector2.Lerp(this.movementComponent.Direction, targetDirection, chaseSmoothing), MovementIntentType.Move));
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") && this.playerTransform == null)
        {
            this.playerTransform = collision.transform;
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") && this.playerTransform != null)
        {
            this.playerTransform = null;
            this.movementComponent.StopMovement();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(this.transform.position, this.chaseRadiusValue);
    }
}
