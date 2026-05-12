using Kope.Actor.New;
using UnityEngine;

namespace Kope.Component {
	public interface IAnimationComponentNew {

		AnimationStatus EvaluateTransitionFeasibility(AnimationStateProfileData animState);
		AnimationStatus PlayAnimation(AnimationStateProfileData animState, bool alreadyChecked = false);
		void SetDefaultSpeed();
		void SetDirection(Vector2 dir);
		bool DoesAnimationExist(int animationHash);
		bool IsAnimationFinished(AnimationStateProfileData animState);
		bool CanTransitionToNextAnimation(int animationHash);
	}
}