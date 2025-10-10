using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public class ChasePlayer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CircleCollider2D chaseRadius;
    [SerializeField] private MovementComponent movementComponent;

    [Header("Chase Settings")]
    [Tooltip("How smoothly the enemy turns toward the player. Lower = slower turn.")]
    [Range(0f, 1f)]
    [SerializeField] private float chaseSmoothing = 0.15f;

    private Transform playerTransform;

    private void Awake()
    {
        if (chaseRadius == null)
            chaseRadius = GetComponent<CircleCollider2D>();

        if (movementComponent == null)
            movementComponent = GetComponent<MovementComponent>();

        chaseRadius.isTrigger = true;
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
            Debug.Log("Player entered chase radius");
            playerTransform = collision.transform;
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") && playerTransform != null)
        {
            Debug.Log("Player exited chase radius");
            playerTransform = null;
            movementComponent.direction = Vector2.zero;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        float radius = chaseRadius != null ? chaseRadius.radius : 1f;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
