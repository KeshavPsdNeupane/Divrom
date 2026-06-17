using System;
using UnityEngine;
namespace Kope.Component.Animation {
	public interface IAnimationComponent {
		event Action OnAnimationTrigger;
		void MoveAnimation(Vector2 dir);
		void SetDefaultAnimationSpeed();
		void AnimationTrigger();
		bool DoesAnimationExist(int animationHash);
		bool IsAnimationFinished(int animationHash, float THRESHOLD = 0.9f);
		bool CanTransitionToNextAnimation(int animationHash);
	}
}