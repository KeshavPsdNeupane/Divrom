using UnityEngine;
using System;

namespace Kope.Actor.New {
	public enum AnimationStatus {
		Success = 0,
		AnimationDoesNotExist = 1,
		AnimationNotFinished = 2,
		AlreadyInTransition = 3
	}
	/// <summary>
	/// Immutable struct to hold animation state data for use in the new animation system. 
	/// </summary>
	[Serializable]
	public readonly struct AnimationStateProfile {
		public const float DEFAULT_ANIMATION_SPEED = 1.0f;
		public const float DEFAULT_NORMALIZED_EXIT_TIME = 0.9f;
		[Tooltip("Name of the animation state as defined in the Animator." +
		"Must be same as the state name in the Unity's Animator.")]
		public readonly string Name { get; }
		public readonly int Hash { get; }
		public readonly float AnimationSpeed { get; }
		public readonly bool IsLooping { get; }
		public readonly float NormalizedExitTime { get; }

		public AnimationStateProfile(string name, float animationSpeed = DEFAULT_ANIMATION_SPEED,
			bool isLooping = false, float normalizedExitTime = DEFAULT_NORMALIZED_EXIT_TIME) {
			Name = name;
			Hash = Animator.StringToHash(name);
			AnimationSpeed = animationSpeed;
			IsLooping = isLooping;
			NormalizedExitTime = Mathf.Clamp01(normalizedExitTime);
		}

		public AnimationStateProfile CopyWith(string name = null, float? animationSpeed = null,
			bool? isLooping = null, float? normalizedExitTime = null) {
			return new AnimationStateProfile(
				name ?? Name,
				animationSpeed ?? AnimationSpeed,
				isLooping ?? IsLooping,
				normalizedExitTime ?? NormalizedExitTime
			);
		}
	}
}
