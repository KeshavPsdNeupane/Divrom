using UnityEngine;

public class MovementComponent : MonoBehaviour
{
    public Rigidbody2D rb;
    [SerializeField] private CharacterStatsSystem characterStats;
    [HideInInspector] public Vector2 direction;
    [HideInInspector] public Vector2 lastDirection;
    [SerializeField]public float movementSpeedUseThisOnlyIfUDontWantToUseCharacterStat = 2.0f;
    [SerializeField] private bool canCapMovementSpeed = false;
    [SerializeField] private float upperLimitMovementSpeed = 10.0f;
    public virtual void ApplyMovement(float movementSpeedMult = 1.0f)
    {
        float mmove = (this.characterStats == null) ? this.movementSpeedUseThisOnlyIfUDontWantToUseCharacterStat
            : this.characterStats.GetStatValue(CharacterStatType.MovementSpeed);

        float movementSpeed = !this.canCapMovementSpeed ? Mathf.Max(0, mmove)
            :Mathf.Clamp(mmove, 0, this.upperLimitMovementSpeed);

        rb.linearVelocity = direction.normalized * movementSpeed * movementSpeedMult;
        if(this.direction.sqrMagnitude > PlayerAnimationThreshold.WALKING_THRESHOLD)
        {
            this.lastDirection = this.direction;
        }
    }



}
