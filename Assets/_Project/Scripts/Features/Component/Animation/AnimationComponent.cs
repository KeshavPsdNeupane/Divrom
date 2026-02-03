using UnityEngine;

using System;
using Kope.Core.CompilerServices;
using Kope.Core.Init;

public class AnimationComponentBase : InitializableBase
{
    // no Awake or Start method here to InterOp with InitCallerManager
    // no no need to add InitializableBase here because no Init logic is needed
    public Animator anim;
    public event Action OnAnimationTrigger;

    public const float DEAFULT_ANIMATION_SPEED = 1.0f;

    public void SetDefaultAnimationSpeed() => this.anim.speed = DEAFULT_ANIMATION_SPEED;

    public override void OnInit()
    {
        base.OnInit();
        /// Defaulting to faceing down on init
        /// this is needed because otherwise the animator will at 0,0 direction 
        /// then when moving it will interpolate from 0,0 to the movement direction
        MoveAnimation(new Vector2(0, -1));

        if (this.anim == null)
        {
            MyLogger.Error("Animator component is not assigned in AnimationComponent." + GetParentGameObjectStackTraceMessage());
        }

    }
    public void AnimationTrigger()
    {
        OnAnimationTrigger?.Invoke();
    }

    public void MoveAnimation(Vector2 dir)
    {
        /// This  is needed to snap the direction to 4 directions (up, down, left, right)
        /// since there is no diagonal movement animation. and it again 
        /// snaps the snapped direction to the closest axis. so unity again wont 
        /// interpolate between two axis. for example if dir is (0.7, 0.3)
        /// it will snap to (1,0) instead of (0.7,0.3), otherwise the animation
        /// will blend between right and up animations. this is undesired.
        Vector2 snapped = dir;
        snapped.x = snapped.x == 0 ? 0 : (Mathf.Abs(snapped.x) >= Mathf.Abs(snapped.y) ? Mathf.Sign(snapped.x) : 0);
        snapped.y = snapped.y == 0 ? 0 : (Mathf.Abs(snapped.y) > Mathf.Abs(snapped.x) ? Mathf.Sign(snapped.y) : 0);

        // Set animator parameters
        this.anim.SetFloat(AnimationVariableHashes.DirectionX, snapped.x);
        this.anim.SetFloat(AnimationVariableHashes.DirectionY, snapped.y);
    }

    public bool DoesAnimationExist(int animationHash)
    => this.anim.HasState(0, animationHash);
    public bool DoesAnimationExist(string animationName)
    => DoesAnimationExist(Animator.StringToHash(animationName));

    public bool IsAnimationFinished(int animationHash, float THRESHOLD = 0.9f)
    {
        AnimatorStateInfo stateInfo = this.anim.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.shortNameHash != animationHash) return false;
        return stateInfo.normalizedTime >= THRESHOLD;
    }

    public bool CanTransitionToAnimation(int animationHash)
    {
        return this.anim.IsInTransition(0) == false &&
               this.anim.GetCurrentAnimatorStateInfo(0).shortNameHash != animationHash;
    }
}