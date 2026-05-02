using UnityEngine;

using System;
using Kope.Core.CompilerServices;
using Kope.Core.Init;


namespace Kope.Component.Animation {

	public class AnimationComponentBase : InitializableBase, IAnimationComponent {
		public Animator anim;
		public event Action OnAnimationTrigger;

		public const float DEFAULT_ANIMATION_SPEED = 1.0f;

		public void SetDefaultAnimationSpeed() => this.anim.speed = DEFAULT_ANIMATION_SPEED;

		protected override bool OnInit() {
			if (this.anim == null) {
				Debug.LogError("Animator component is not assigned in AnimationComponent." + GetParentGameObjectHeirarchyMessage());
				return false;
			}
			/// Defaulting to faceing down on init
			/// this is needed because otherwise the animator will at 0,0 direction 
			/// then when moving it will interpolate from 0,0 to the movement direction
			MoveAnimation(new Vector2(0, -1));
			return true;

		}
		public void AnimationTrigger() {
			OnAnimationTrigger?.Invoke();
		}

		public void MoveAnimation(Vector2 dir) {
			/// This  is needed to snap the direction to 4 directions (up, down, left, right)
			/// since there is no diagonal movement animation. and it again 
			/// snaps the snapped direction to the closest axis. so unity again wont 
			/// interpolate between two axis. for example if dir is (0.7, 0.3)
			/// it will snap to (1,0) instead of (0.7,0.3), otherwise the animation
			/// will blend between right and up animations. this is undesired.
			Vector2 snapped = new() {
				x = dir.x == 0 ? 0 : (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y) ? Mathf.Sign(dir.x) : 0),
				y = dir.y == 0 ? 0 : (Mathf.Abs(dir.y) > Mathf.Abs(dir.x) ? Mathf.Sign(dir.y) : 0)
			};

			// Set animator parameters
			this.anim.SetFloat(AnimationVariableHashes.DirectionX, snapped.x);
			this.anim.SetFloat(AnimationVariableHashes.DirectionY, snapped.y);
		}

		public bool DoesAnimationExist(int animationHash)
		=> this.anim.HasState(0, animationHash);

		public bool IsAnimationFinished(int animationHash, float THRESHOLD = 0.9f) {
			AnimatorStateInfo stateInfo = this.anim.GetCurrentAnimatorStateInfo(0);
			if (stateInfo.shortNameHash != animationHash) return false;
			return stateInfo.normalizedTime >= THRESHOLD;
		}

		public bool CanTransitionToNextAnimation(int nextAnimationHash) {
			// animation can be transitioned to if the animator is not in transition and
			// the current animation is not the same as the target animation
			return this.anim.IsInTransition(0) == false &&
				   this.anim.GetCurrentAnimatorStateInfo(0).shortNameHash != nextAnimationHash;
		}

	}
}