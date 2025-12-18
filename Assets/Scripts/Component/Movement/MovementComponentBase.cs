using UnityEngine;

public abstract class MovementComponentBase : InitializableBase
{
    [SerializeField] protected Rigidbody2D rb;
    [SerializeField] protected CharacterStatsSystem characterStatsSystem;
    [SerializeField] protected float movementSpeed = 2f;

    protected Vector2 direction;
    protected Vector2 lastDirection;

    public Vector2 Direction => this.direction;

    public override void Init()
    {
        if (this.characterStatsSystem == null)
        {
            Debug.LogWarning("CharacterStatsSystem not assigned in MovementComponentBase, trying to get it from the GameObject.");
            this.characterStatsSystem = GetComponent<CharacterStatsSystem>();
        }
        SetInitialized();
    }

    protected virtual void OnEnable() => SubscribeToStats();
    protected virtual void OnDisable() => UnsubscribeFromStats();

    private void SubscribeToStats()
    {
        print("Called SubscribeToStats");
        if (this.characterStatsSystem != null &&
            this.characterStatsSystem.CurrentStats != null)
        {
            this.characterStatsSystem.StatsSubscribe(CharacterStatType.SPD, SetMovementSpeed);
            // Initial fetch 
            SetMovementSpeed(this.characterStatsSystem.CurrentStats[CharacterStatType.SPD].GetValue());
        }
    }
    private void UnsubscribeFromStats()
    {
        if (this.characterStatsSystem != null &&
            this.characterStatsSystem.CurrentStats != null)
        {
            this.characterStatsSystem.StatsUnsubscribe(CharacterStatType.SPD, SetMovementSpeed);
        }
    }

    // Optional helper for setting speed
    public virtual void SetMovementSpeed(float speed)
    {
        this.movementSpeed = speed;
    }

    public virtual void SetDirection(Vector2 newDirection)
    {
        this.direction = newDirection;

        if (direction.sqrMagnitude > AnimationThreshold.WALKING_THRESHOLD) lastDirection = direction;
    }

    // This replaces interface method
    public abstract void ApplyMovement(float multiplier = 1f);
}
