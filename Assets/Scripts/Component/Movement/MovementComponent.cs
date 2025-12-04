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


    private void MovementSpeedCallBack(float newMovementSpeed)
    => this.movementSpeed = newMovementSpeed;
    private void Awake()
    {
        if (this.characterStats == null)
        {
            this.characterStats = GetComponent<CharacterStatsSystem>();
        }
    }

    private void Start()
    {
        if (this.characterStats != null)
        {
            this.characterStats.StatsSubscribe(CharacterStatType.MovementSpeed, MovementSpeedCallBack);
            // Initial fetch
            MovementSpeedCallBack(this.characterStats.currentStats[CharacterStatType.MovementSpeed].GetValue());
        }

    }

    private void OnDisable()
    {
        if (this.characterStats != null)
        {
            this.characterStats.StatsUnsubscribe(CharacterStatType.MovementSpeed, MovementSpeedCallBack);
        }
    }

    public virtual void ApplyMovement(float movementSpeedMult = 1.0f)
    {
        float camMoveSpeed = (this.characterStats == null)
            ? this.movementSpeedUseThisOnlyIfUDontWantToUseCharacterStat
            : this.movementSpeed;

        float movementSpeed = !this.canCapMovementSpeed ? Mathf.Max(0, camMoveSpeed)
            : Mathf.Clamp(camMoveSpeed, 0, this.upperLimitMovementSpeed);

        this.rb.linearVelocity = movementSpeed * movementSpeedMult * direction.normalized;
        if (this.direction.sqrMagnitude > PlayerAnimationThreshold.WALKING_THRESHOLD)
        {
            this.lastDirection = this.direction;
        }
    }



}
