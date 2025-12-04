using UnityEngine;

public class MovementComponent : MonoBehaviour
{
    public Rigidbody2D rb;
    [SerializeField] private CharacterStatsSystem characterStats;
    [HideInInspector] public Vector2 direction;
    [HideInInspector] public Vector2 lastDirection;
    [SerializeField] public float movementSpeedUseThisOnlyIfUDontWantToUseCharacterStat = 2.0f;
    private float movementSpeed;
    [SerializeField] private bool canCapMovementSpeed = false;
    [SerializeField] private float upperLimitMovementSpeed = 10.0f;

    private void Awake()
    {
        if (this.characterStats == null)
        {
            Debug.LogWarning("CharacterStatsSystem not assigned in MovementComponent, trying to get it from the GameObject.");
            this.characterStats = GetComponent<CharacterStatsSystem>();
        }
    }

    private void Start() => SubscribeToMovementSpeed();

    void OnEnable() => SubscribeToMovementSpeed();

    void OnDisable() => UnsubscribeFromMovementSpeed();


    private void SubscribeToMovementSpeed()
    {
        if (this.characterStats != null &&
            this.characterStats.currentStats != null &&
            this.characterStats.currentStats.ContainsKey(CharacterStatType.SPD))
        {
            this.characterStats.StatsSubscribe(CharacterStatType.SPD, MovementSpeedCallBack);
            // Initial fetch
            MovementSpeedCallBack(this.characterStats.currentStats[CharacterStatType.SPD].GetValue());
        }
    }

    private void UnsubscribeFromMovementSpeed()
    {
        if (this.characterStats != null &&
           this.characterStats.currentStats != null &&
            this.characterStats.currentStats.ContainsKey(CharacterStatType.SPD))
        {
            this.characterStats.StatsUnsubscribe(CharacterStatType.SPD, MovementSpeedCallBack);
        }
    }

    private void MovementSpeedCallBack(float newMovementSpeed)
        => this.movementSpeed = newMovementSpeed;

    public virtual void ApplyMovement(float movementSpeedMult = 1.0f)
    {
        float camMoveSpeed = (this.characterStats == null)
            ? movementSpeedUseThisOnlyIfUDontWantToUseCharacterStat
            : movementSpeed;

        float finalSpeed = !canCapMovementSpeed
            ? Mathf.Max(0, camMoveSpeed)
            : Mathf.Clamp(camMoveSpeed, 0, upperLimitMovementSpeed);

        this.rb.linearVelocity = finalSpeed * movementSpeedMult * direction.normalized;

        if (direction.sqrMagnitude > PlayerAnimationThreshold.WALKING_THRESHOLD)
            lastDirection = direction;
    }
}
