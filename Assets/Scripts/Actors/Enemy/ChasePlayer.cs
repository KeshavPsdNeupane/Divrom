using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public class ChasePlayer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CircleCollider2D chaseRadius;
    [SerializeField] private MovementComponent movementComponent;
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
            this.movementComponent = GetComponent<MovementComponent>();

        this.chaseRadius.isTrigger = true;
        this.chaseRadius.radius = chaseRadiusValue;
    }

    private void Update()
    {
        if (playerTransform == null)
        {
            movementComponent.direction = Vector2.zero;
            return;
        }

        Vector2 targetDirection = (playerTransform.position - transform.position).normalized;
        movementComponent.direction = Vector2.Lerp(
            movementComponent.direction,
            targetDirection,
            chaseSmoothing
        );
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") && playerTransform == null)
        {
            playerTransform = collision.transform;
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") && playerTransform != null)
        {
            playerTransform = null;
            movementComponent.direction = Vector2.zero;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        float radius = chaseRadius != null ? this.chaseRadius.radius : 1f;
        Gizmos.DrawWireSphere(this.transform.position, radius);
    }
}
