using Kope.Core.CompilerServices;
using UnityEngine;
using Kope.Core.Init;
public class MovementComponentBase : InitializableBase
{
    [SerializeField] protected Rigidbody2D rb;
    [SerializeField] protected CharacterStatsSystem characterStatsSystem;
    [SerializeField] protected float defaultMovementSpeed = 2f;

    public const float DIRECTION_THRESHOLD = 0.1f;
    protected bool directionChanged;
    protected Vector2 normalizedDirection;

    protected Vector2 direction = Vector2.zero;
    protected Vector2 lastDirection = Vector2.zero;


    public Vector2 Direction => this.direction;

    public override void Init()
    {
        if (this.characterStatsSystem == null)
        {
            this.characterStatsSystem = GetComponent<CharacterStatsSystem>();
            MyLogger.Warn($"MovementComponentBase ({gameObject.name}): " +
           "CharacterStatsSystem not assigned, attempting to fetch from same GameObject.");
        }
        SetInitialized();
    }

    protected virtual void OnEnable() => SubscribeToStats();
    protected virtual void OnDisable() => UnsubscribeFromStats();

    private void SubscribeToStats()
    {
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
        this.defaultMovementSpeed = speed;
    }

    public virtual void SetDirection(Vector2 newDirection)
    {
        this.direction = newDirection;
        this.directionChanged = true;

        if (direction.sqrMagnitude > DIRECTION_THRESHOLD)
            lastDirection = direction;
    }
    public virtual void ApplyMovement(float movementSpeedMult = 1.0f)
    {
        if (this.directionChanged)
        {
            this.normalizedDirection = this.direction.normalized;
            this.directionChanged = false;
        }

        this.rb.linearVelocity = defaultMovementSpeed * movementSpeedMult * normalizedDirection;
    }
}
