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
	/// A read-only data container representing an animation state configuration.
	/// </summary>
	/// <remarks>
	/// <b>Note:</b> This struct is not Unity-serializable due to private setters. 
	/// To edit these values via the Unity Inspector, create a serializable wrapper class.
	/// This design ensures immutability; instances can only be created via the constructor 
	/// or modified via the <see cref="CopyWith"/> method.
	/// </remarks>
	public struct AnimationStateProfileData {
		public const float DEFAULT_ANIMATION_SPEED = 1.0f;
		public const float DEFAULT_NORMALIZED_EXIT_TIME = 0.9f;

		public string Name { get; private set; }
		public float AnimationSpeed { get; private set; }
		public bool IsLooping { get; private set; }
		public float NormalizedExitTime { get; private set; }

		private readonly int _hash;
		public readonly int Hash => this._hash;

		/// <summary>
		/// Initializes a new instance of the AnimationStateProfileData.
		/// </summary>
		public AnimationStateProfileData(string name, float animationSpeed = DEFAULT_ANIMATION_SPEED,
			bool isLooping = false, float normalizedExitTime = DEFAULT_NORMALIZED_EXIT_TIME) {
			this.Name = name;
			this.AnimationSpeed = animationSpeed;
			this.IsLooping = isLooping;
			this.NormalizedExitTime = Mathf.Clamp01(normalizedExitTime);
			this._hash = Animator.StringToHash(name);
		}

		/// <summary>
		/// Deep copy constructor.
		/// </summary>
		public AnimationStateProfileData(AnimationStateProfileData profile) {
			this.Name = profile.Name;
			this.AnimationSpeed = profile.AnimationSpeed;
			this.IsLooping = profile.IsLooping;
			this.NormalizedExitTime = Mathf.Clamp01(profile.NormalizedExitTime);
			this._hash = profile._hash;
		}

		/// <summary>
		/// Creates a new copy of the profile with optionally modified parameters.
		/// </summary>
		/// <returns>A new instance of <see cref="AnimationStateProfileData"/> with updated values.</returns>
		public readonly AnimationStateProfileData CopyWith(
			string name = null,
			float? animationSpeed = null,
			bool? isLooping = null,
			float? normalizedExitTime = null) {
			return new AnimationStateProfileData(
				name ?? Name,
				animationSpeed ?? AnimationSpeed,
				isLooping ?? IsLooping,
				normalizedExitTime ?? NormalizedExitTime
			);
		}

		public override readonly string ToString() {
			return $"AnimationStateProfile(Name: {Name}, AnimationSpeed: {AnimationSpeed}, " +
				   $"IsLooping: {IsLooping}, NormalizedExitTime: {NormalizedExitTime},Hash: {Hash})";
		}
	}
}
