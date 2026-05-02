using Kope.Actor.New;
using Kope.Core.Init;
using UnityEngine;

namespace Kope.Component {
	public class AnimationComponentBaseNew : InitializableBase, IAnimationComponentNew {
		[SerializeField] protected Animator animator;

		protected override bool OnInit() {
			if (!animator) {
				Debug.LogError($"Animator unassigned on {gameObject.name}. {GetParentGameObjectHeirarchyMessage()}");
				return false;
			}

			// Defaulting to facing down prevents weird interpolation from (0,0) on start
			SetDirection(Vector2.down);
			return true;
		}

		public AnimationStatus PlayAnimation(AnimationStateProfile animState) {
			if (!DoesAnimationExist(animState.Hash)) return AnimationStatus.AnimationDoesNotExist;

			var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
			bool isSameAnimation = stateInfo.shortNameHash == animState.Hash;

			// Only block "Not Finished" if the animation is NOT a loop.
			// This allows movement loops to be interrupted instantly by other states.
			if (!animState.IsLooping && isSameAnimation) {
				if ((stateInfo.normalizedTime % 1f) < animState.NormalizedExitTime) {
					return AnimationStatus.AnimationNotFinished;
				}
			}

			if (animator.IsInTransition(0))
				return AnimationStatus.AlreadyInTransition;

			this.animator.speed = animState.AnimationSpeed;
			this.animator.Play(animState.Hash);
			return AnimationStatus.Success;
		}

		public void SetDirection(Vector2 dir) {
			if (dir == Vector2.zero) return;

			// Snap to 4-way cardinal grid to prevent Blend Tree "ghosting" or diagonal frames
			Vector2 snapped = Mathf.Abs(dir.x) >= Mathf.Abs(dir.y)
				? new Vector2(Mathf.Sign(dir.x), 0)
				: new Vector2(0, Mathf.Sign(dir.y));

			this.animator.SetFloat(AnimationVariableHashes.DirectionX, snapped.x);
			this.animator.SetFloat(AnimationVariableHashes.DirectionY, snapped.y);
		}

		public void SetDefaultSpeed() => this.animator.speed = AnimationStateProfile.DEFAULT_ANIMATION_SPEED;

		public bool DoesAnimationExist(int hash) => this.animator.HasState(0, hash);

		public bool IsAnimationFinished(AnimationStateProfile animState) {
			var stateInfo = this.animator.GetCurrentAnimatorStateInfo(0);
			if (stateInfo.shortNameHash != animState.Hash) return false;
			if (animState.IsLooping) {
				// Loop is considered "finished" after a full cycle
				return stateInfo.normalizedTime >= 1f;
			}
			return stateInfo.normalizedTime >= animState.NormalizedExitTime;
		}

		public bool CanTransitionToNextAnimation(int hash) =>
			!this.animator.IsInTransition(0) &&
			this.animator.GetCurrentAnimatorStateInfo(0).shortNameHash != hash;
	}
}