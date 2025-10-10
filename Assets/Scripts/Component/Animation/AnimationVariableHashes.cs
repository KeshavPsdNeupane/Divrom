using UnityEngine;

public readonly struct AnimationVariableHashes
{
    public static readonly int DirectionX = Animator.StringToHash("DirectionX");
    public static readonly int DirectionY = Animator.StringToHash("DirectionY");
    public static readonly int IsWalking = Animator.StringToHash("IsWalking");
    public static readonly int IsBasicAttacking = Animator.StringToHash("IsBasicAttacking");
}