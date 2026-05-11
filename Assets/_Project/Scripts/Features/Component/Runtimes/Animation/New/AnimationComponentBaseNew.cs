using Kope.Actor.New;
using Kope.Core.Init;
using UnityEngine;

namespace Kope.Component {
	public class AnimationComponentBaseNew : InitializableBase, IAnimationComponentNew {
		[SerializeField] protected Animator animator;
		protected override bool OnInit() {
			if (!this.animator) return false;
			SetDirection(Vector2.down);
			return true;
		}

		public AnimationStatus PlayAnimation(AnimationStateProfileData animState) {
			if (!DoesAnimationExist(animState.Hash)) return AnimationStatus.NotFound;

			var info = this.animator.GetCurrentAnimatorStateInfo(0);
			bool isSame = info.shortNameHash == animState.Hash;

			// Block if the current non-looping animation hasn't hit exit time
			if (!animState.IsLooping && isSame) {
				if ((info.normalizedTime % 1f) < animState.NormalizedExitTime) {
					return AnimationStatus.Busy;
				}
			}

			if (this.animator.IsInTransition(0)) return AnimationStatus.InTransition;

			this.animator.speed = animState.AnimationSpeed;
			this.animator.Play(animState.Hash);
			return AnimationStatus.Success;
		}

		public void SetDirection(Vector2 dir) {
			if (dir == Vector2.zero) return;
			Vector2 snapped = Mathf.Abs(dir.x) >= Mathf.Abs(dir.y)
				? new Vector2(Mathf.Sign(dir.x), 0)
				: new Vector2(0, Mathf.Sign(dir.y));

			this.animator.SetFloat(AnimationVariableHashes.DirectionX, snapped.x);
			this.animator.SetFloat(AnimationVariableHashes.DirectionY, snapped.y);
		}

		public bool DoesAnimationExist(int hash) => this.animator.HasState(0, hash); public void SetDefaultSpeed() => this.animator.speed = AnimationStateProfileData.DEFAULT_ANIMATION_SPEED;


		public bool IsAnimationFinished(AnimationStateProfileData animState) {
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