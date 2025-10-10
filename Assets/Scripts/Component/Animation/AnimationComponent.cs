using UnityEngine;

using System;
public class AnimationComponent : MonoBehaviour
{
    public Animator anim;
    public event Action OnAnimationTrigger;

    public void AnimationTrigger()
    {
        OnAnimationTrigger?.Invoke();
    }

    public void MoveAnimation(Vector2 direction)
    {
        this.anim.SetFloat(AnimationVariableHashes.DirectionX, direction.x);
        this.anim.SetFloat(AnimationVariableHashes.DirectionY, direction.y);

    }
}
