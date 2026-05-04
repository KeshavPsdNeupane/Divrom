using Kope.Actor.New;
using UnityEngine;

namespace Kope.Component {
	public interface IAnimationComponentNew {

		/// <summary>
		/// Attempts to play the specified animation state.
		/// </summary>
		/// <param name="animState">The profile defining the animation state to play.</param>
		/// <returns>An <see cref="AnimationStatus"/> detailing whether the transition started or 
		/// why it failed.</returns>
		/// <remarks>
		/// This method follows a non-blocking pattern, allowing callers to either "fire and forget" 
		/// or implement custom logic (e.g., queuing or logging) based on the returned status.
		/// </remarks>
		AnimationStatus PlayAnimation(AnimationStateProfileData animState);

		void SetDefaultSpeed();
		void SetDirection(Vector2 dir);
		bool DoesAnimationExist(int animationHash);
		bool IsAnimationFinished(AnimationStateProfileData animState);
		bool CanTransitionToNextAnimation(int animationHash);
	}
}