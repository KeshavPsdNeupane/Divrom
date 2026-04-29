using UnityEngine;
using System;

namespace Kope.Actor.New {
	/// <summary>
	/// Immutable struct to hold animation state data for use in the new animation system. 
	/// </summary>
	[Serializable]
	public readonly struct AnimationStateProfile {
		public const float DEFAULT_NORMALIZED_EXIT_TIME = 0.9f;
		public readonly string Name { get; }
		public readonly int Hash { get; }
		public readonly float AnimationSpeed { get; }
		public readonly float NormalizedExitTime { get; }
		public AnimationStateProfile(string name, float animationSpeed,
		 float normalizedExitTime = DEFAULT_NORMALIZED_EXIT_TIME) {
			this.Name = name;
			this.Hash = Animator.StringToHash(name);
			// Ensure normalized exit time is between 0 and 1
			this.NormalizedExitTime = Mathf.Clamp01(normalizedExitTime);
			this.AnimationSpeed = animationSpeed;
		}
		public AnimationStateProfile CopyWith(string name = null,
		float? animationSpeed = null, float? normalizedExitTime = null) {
			return new AnimationStateProfile(
				name ?? this.Name,
				animationSpeed ?? this.AnimationSpeed,
				normalizedExitTime ?? this.NormalizedExitTime
			);

		}
	}
}
