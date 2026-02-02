using Kope.Core.CompilerServices;
using UnityEngine;
using Kope.Core.Init;
using Kope.Character.Stats;


namespace Kope.Component.Movement
{

    /// <summary>
    /// Defines the type of movement intent.
    /// Just a simple enum to indicate what kind of movement is intended.
    /// </summary>
    public enum MovementIntentType
    {
        Stop = 0,
        Move = 10,

    }



    public struct MovementIntent
    {
        public Vector2 Direction;

        /// <summary>
        /// Must be between 0 and 1.
        /// Speed boost should be handled on Stats level, not here.
        /// This mult is used to scale the movement speed as needed.
        /// For example, a value of 1.0 is normal speed for movement,
        /// we can use like 0.5 for movement speed when we are attacking,
        /// and so on.
        /// </summary>
        public MovementIntentType IntentType;
        public float intentSpeedMultiplier;
        public MovementIntent(Vector2 direction, MovementIntentType intentType = MovementIntentType.Stop, float speedMultiplier = 1f)
        {
            this.Direction = direction;
            this.IntentType = intentType;
            this.intentSpeedMultiplier = Mathf.Clamp01(speedMultiplier);
        }
    }



    public class MovementComponentBase : InitializableBase
    {
        [SerializeField] protected Rigidbody2D rb;
        [SerializeField] protected CharacterStatsSystem characterStatsSystem;
        [SerializeField] protected float defaultMovementSpeed = 2f;

        /// <summary>
        /// this is universal threshold to determine if direction is significant enough to consider.
        /// so no need to square it every time.
        /// </summary>
        public const float MOVEMENT_EPSILON = 0.1f;

        protected MovementIntent currentIntent;
        public Rigidbody2D Rigidbody => this.rb;
        public Vector2 Direction => this.currentIntent.Direction;
        public Vector2 Position => this.rb.position;
        public override void Init()
        {
            if (this.IsInitialized) return;
            base.Init();
            if (this.characterStatsSystem == null)
            {
                this.characterStatsSystem = GetComponent<CharacterStatsSystem>();
                MyLogger.Warn($"MovementComponentBase ({gameObject.name}): " +
               "CharacterStatsSystem not assigned, attempting to fetch from same GameObject.");
            }
        }

        protected virtual void OnEnable() => SubscribeToStats();
        protected virtual void OnDisable()
        {
            UnsubscribeFromStats();
            StopMovement();
        }

        private void SubscribeToStats()
        {
            if (this.characterStatsSystem != null &&
                this.characterStatsSystem.CurrentStats != null)
            {
                this.characterStatsSystem.StatsSubscribe(CharacterStatType.SPD, SetDefaultMovementSpeed);
                // Initial fetch 
                SetDefaultMovementSpeed(this.characterStatsSystem.CurrentStats[CharacterStatType.SPD].GetValue());
            }
        }
        private void UnsubscribeFromStats()
        {
            if (this.characterStatsSystem != null &&
                this.characterStatsSystem.CurrentStats != null)
            {
                this.characterStatsSystem.StatsUnsubscribe(CharacterStatType.SPD, SetDefaultMovementSpeed);
            }
        }

        protected virtual void FixedUpdate()
        {
            ApplyPhysics();
        }


        /// <summary>
        /// Sets the default movement speed.
        /// This is usually called by the CharacterStatsSystem when the SPD stat changes.
        /// </summary>
        public virtual void SetDefaultMovementSpeed(float speed)
        => this.defaultMovementSpeed = speed;

        /// <summary>
        /// Sets the movement intent for this component.
        /// The intent direction will be normalized if its magnitude is greater than the direction epsilon.
        /// if u wanna do some fancy with direction, just lerp or slerp or whatever before passing it here.
        /// this function will just assign the direction to velocity after normalization. 
        /// it does not do any smoothing or interpolation.
        /// </summary>
        /// <param name="intent"></param>
        public virtual void SetMovementIntent(MovementIntent intent)
        {
            if (intent.Direction.sqrMagnitude > MOVEMENT_EPSILON)
            {
                intent.Direction.Normalize();
            }
            else
            {
                intent.Direction = Vector2.zero;
            }
            this.currentIntent = intent;
        }

        public void StopMovement()
        {
            this.currentIntent = default;
        }


        protected virtual void ApplyPhysics()
        {
            Vector2 targetVelocity = Vector2.zero;
            if (this.currentIntent.IntentType != MovementIntentType.Stop)
            {
                targetVelocity = this.currentIntent.intentSpeedMultiplier * this.defaultMovementSpeed * this.currentIntent.Direction;
            }

            // Blend physics velocity (from collisions) with desired velocity
            // This allows entities to push each other while maintaining responsive control
            Vector2 physicsInfluence = this.rb.linearVelocity * 0.3f; // Preserve collision response
            Vector2 intentInfluence = targetVelocity * 0.7f;

            this.rb.linearVelocity = physicsInfluence + intentInfluence;
        }
    }

}