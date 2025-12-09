using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
public class MovementComponent : InitializableBase
{
    public Rigidbody2D rb;
    [SerializeField] private CharacterStatsSystem characterStats;
    [HideInInspector] public Vector2 direction;
    [HideInInspector] public Vector2 lastDirection;
    [SerializeField] public float movementSpeedUseThisOnlyIfUDontWantToUseCharacterStat = 2.0f;
    private float movementSpeed;
    [SerializeField] private bool canCapMovementSpeed = false;
    [SerializeField] private float upperLimitMovementSpeed = 10.0f;

    public override void Init()
    {
        if (this.characterStats == null)
        {
            Debug.LogWarning("CharacterStatsSystem not assigned in MovementComponent, trying to get it from the GameObject.");
            this.characterStats = GetComponent<CharacterStatsSystem>();
        }
        SetInitialized();
    }
    //void Start() => SubscribeToMovementSpeed(); 
    void OnEnable() => SubscribeToMovementSpeed();

    void OnDisable() => UnsubscribeFromMovementSpeed();


    private void SubscribeToMovementSpeed()
    {
        if (this.characterStats != null &&
            this.characterStats.CurrentStats != null)
        {
            this.characterStats.StatsSubscribe(CharacterStatType.SPD, MovementSpeedCallBack);
            // Initial fetch
            MovementSpeedCallBack(this.characterStats.CurrentStats[CharacterStatType.SPD].GetValue());
        }
    }

    private void UnsubscribeFromMovementSpeed()
    {
        if (this.characterStats != null &&
           this.characterStats.CurrentStats != null)
        {
            this.characterStats.StatsUnsubscribe(CharacterStatType.SPD, MovementSpeedCallBack);
        }
    }

    private void MovementSpeedCallBack(float newMovementSpeed)
        => this.movementSpeed = newMovementSpeed;


    // putting it here so other systems can call it too
    // e.g. AI movement system
    // or other player movement systems
    // this decouples State Controller from movement logic
    public void MoveForInputSystem(InputAction.CallbackContext context)
    {
        this.direction = context.ReadValue<Vector2>();
    }


    public virtual void ApplyMovement(float movementSpeedMult = 1.0f)
    {
        float canMoveSpeed = (this.characterStats == null)
            ? movementSpeedUseThisOnlyIfUDontWantToUseCharacterStat
            : movementSpeed;

        float lowerLimitMovementSpeed = 0f;

        float finalSpeed = !canCapMovementSpeed
            ? Mathf.Max(lowerLimitMovementSpeed, canMoveSpeed)
            : Mathf.Clamp(canMoveSpeed, lowerLimitMovementSpeed, upperLimitMovementSpeed);

        this.rb.linearVelocity = finalSpeed * movementSpeedMult * direction.normalized;

        if (direction.sqrMagnitude > AnimationThreshold.WALKING_THRESHOLD)
            lastDirection = direction;
    }
}
