using Kope.Actor.States;
using UnityEngine;

namespace Kope.Component {
	public interface IAnimationComponent {

		AnimationStatus EvaluateTransitionFeasibility(AnimationStateProfileData animState);
		AnimationStatus PlayAnimation(AnimationStateProfileData animState, bool alreadyChecked = false);
		void SetDefaultSpeed();
		void SetDirection(Vector2 dir);
		bool DoesAnimationExist(int animationHash);
		bool IsAnimationFinished(AnimationStateProfileData animState);
		bool CanTransitionToNextAnimation(int animationHash);
	}
}