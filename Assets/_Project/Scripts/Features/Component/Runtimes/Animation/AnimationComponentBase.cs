using Kope.Actor.New;
using Kope.Core.Init;
using UnityEngine;

namespace Kope.Component.Animation {

	public class AnimationComponentBase : InitializableBase, IAnimationComponentNew {
		[SerializeField] protected Animator animator;

		[Header("Enter the name of the string of direction \nfloat from the animator")]
		[SerializeField] protected string directionX = "DirectionX";
		[SerializeField] protected string directionY = "DirectionY";

		private Vector2Int _directionStringHashes;

		protected override bool OnInit() {
			if (!this.animator) return false;
			this._directionStringHashes.x = Animator.StringToHash(this.directionX);
			this._directionStringHashes.y = Animator.StringToHash(this.directionY);
			SetDirection(Vector2.down);
			return true;
		}
		/// <summary>
		/// Executes a state change on the underlying Animator using the provided profile data.
		/// </summary>
		/// <param name="animState">The data profile containing the animation hash, speed, and timing constraints.</param>
		/// <param name="alreadyChecked">
		/// If <see langword="true"/>, bypasses <see cref="EvaluateTransitionFeasibility"/> safety checks. 
		/// Use this to optimize performance when the transition has already been validated by a State Machine 
		/// or Pre-flight check.
		/// </param>
		/// <returns>
		/// Returns <see cref="AnimationStatus.Success"/> if the play command was issued. 
		/// Otherwise, returns the specific failure reason (e.g., Busy, NotFound).
		/// </returns>
		/// <remarks>
		/// <para><b>Caution:</b> Setting <paramref name="alreadyChecked"/> to true assumes the caller 
		/// has verified that the animation exists and that current non-looping states have reached their 
		/// <c>NormalizedExitTime</c>.</para>
		/// </remarks>
		public AnimationStatus PlayAnimation(AnimationStateProfileData animState, bool alreadyChecked = false) {
			if (!alreadyChecked) {
				var validity = EvaluateTransitionFeasibility(animState);
				if (validity != AnimationStatus.Success) return validity;
			}

			this.animator.speed = animState.AnimationSpeed;
			this.animator.Play(animState.Hash);
			return AnimationStatus.Success;
		}

		public AnimationStatus EvaluateTransitionFeasibility(AnimationStateProfileData animState) {
			if (!DoesAnimationExist(animState.Hash)) return AnimationStatus.NotFound;
			var info = this.animator.GetCurrentAnimatorStateInfo(0);
			bool isSame = info.shortNameHash == animState.Hash;
			// block is current is same non-looping animation and hasn't reached exit time yet.
			// if loping then we only check if it's the same, since it can be interrupted at any time.
			if (isSame && !animState.IsLooping) {
				if ((info.normalizedTime % 1f) < animState.NormalizedExitTime) {
					return AnimationStatus.Busy;
				}
			}

			if (this.animator.IsInTransition(0)) return AnimationStatus.InTransition;
			return AnimationStatus.Success;
		}


		public void SetDirection(Vector2 dir) {
			// fixing the direction to the cardinal direction
			if (dir == Vector2.zero) return;
			Vector2 snapped = Mathf.Abs(dir.x) >= Mathf.Abs(dir.y)
				? new Vector2(Mathf.Sign(dir.x), 0)
				: new Vector2(0, Mathf.Sign(dir.y));

			this.animator.SetFloat(this._directionStringHashes.x, snapped.x);
			this.animator.SetFloat(this._directionStringHashes.y, snapped.y);
		}

		public bool DoesAnimationExist(int hash) =>
			this.animator.HasState(0, hash);
		public void SetDefaultSpeed() =>
			this.animator.speed = AnimationStateProfileData.DEFAULT_ANIMATION_SPEED;


		public bool IsAnimationFinished(AnimationStateProfileData animState) {
			var stateInfo = this.animator.GetCurrentAnimatorStateInfo(0);
			if (stateInfo.shortNameHash != animState.Hash) return false;
			if (!animState.IsLooping) {
				// For non-looping animations, we check if the normalized time 
				// has reached or exceeded the exit time.
				return stateInfo.normalizedTime >= animState.NormalizedExitTime;
			}
			return true; // Looping animations are always "finished" since they can be interrupted at any time
		}

		public bool CanTransitionToNextAnimation(int hash) =>
			!this.animator.IsInTransition(0) &&
			this.animator.GetCurrentAnimatorStateInfo(0).shortNameHash != hash;
	}
}