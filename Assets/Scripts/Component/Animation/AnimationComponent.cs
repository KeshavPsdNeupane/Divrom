using UnityEngine;

using System;
public class AnimationComponent : MonoBehaviour
{
    // no Awake or Start method here to InterOp with InitCallerManager
    // no no need to add InitializableBase here because no Init logic is needed
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
