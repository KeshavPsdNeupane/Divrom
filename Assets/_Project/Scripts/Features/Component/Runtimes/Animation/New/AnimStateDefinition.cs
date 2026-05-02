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
	public struct AnimationStateProfile {
		public const float DEFAULT_ANIMATION_SPEED = 1.0f;
		public const float DEFAULT_NORMALIZED_EXIT_TIME = 0.9f;

		[SerializeField] private string name;
		[SerializeField] private float animationSpeed;
		[SerializeField] private bool isLooping;
		[SerializeField, Range(0.0f, 1.0f)] private float normalizedExitTime;

		private int _hash;

		public readonly string Name => this.name;
		public int Hash {
			get {
				if (this._hash == 0) {
					this._hash = Animator.StringToHash(Name);
				}
				return _hash;
			}
		}
		public readonly float AnimationSpeed => animationSpeed;
		public readonly bool IsLooping => isLooping;
		public readonly float NormalizedExitTime => normalizedExitTime;

		public AnimationStateProfile(string name, float animationSpeed = DEFAULT_ANIMATION_SPEED,
			bool isLooping = false, float normalizedExitTime = DEFAULT_NORMALIZED_EXIT_TIME) {
			this.name = name;
			this.animationSpeed = animationSpeed;
			this.isLooping = isLooping;
			this.normalizedExitTime = Mathf.Clamp01(normalizedExitTime);
			_hash = Animator.StringToHash(name);
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

		public override readonly string ToString() {
			return $"AnimationStateProfile(Name: {Name}, AnimationSpeed: {AnimationSpeed}, " +
				   $"IsLooping: {IsLooping}, NormalizedExitTime: {NormalizedExitTime})";
		}
	}
}
