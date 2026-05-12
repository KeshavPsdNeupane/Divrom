using UnityEngine;

namespace Kope.Actor.New {
	public enum AnimationStatus : byte {
		Success = 0,
		NotFound = 1,
		InTransition = 2,
		Busy = 3,       // Animation hasn't reached ExitTime
		Failed = 99
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
		public float AnimationLength { get; private set; }
		public bool IsOneShot { get; private set; }
		public float NormalizedExitTime { get; private set; }

		public readonly float ApprarentAnimationLength {
			get {
				if (Mathf.Approximately(this.AnimationSpeed, 0f)) return float.PositiveInfinity;
				return Mathf.Abs(this.AnimationLength / this.AnimationSpeed);
			}
		}

		private readonly int _hash;
		public readonly int Hash => this._hash;

		/// <summary>
		/// Initializes a new instance of the AnimationStateProfileData.
		/// </summary>
		public AnimationStateProfileData(string name, float animationLength, float animationSpeed = DEFAULT_ANIMATION_SPEED,
			bool isOneShot = false, float normalizedExitTime = DEFAULT_NORMALIZED_EXIT_TIME) {
			this.Name = name;
			this.AnimationSpeed = animationSpeed;
			this.AnimationLength = animationLength;
			this.IsOneShot = isOneShot;
			this.NormalizedExitTime = Mathf.Clamp01(normalizedExitTime);
			this._hash = Animator.StringToHash(name);
		}

		/// <summary>
		/// Deep copy constructor.
		/// </summary>
		public AnimationStateProfileData(AnimationStateProfileData profile) {
			this.Name = profile.Name;
			this.AnimationSpeed = profile.AnimationSpeed;
			this.AnimationLength = profile.AnimationLength;
			this.IsOneShot = profile.IsOneShot;
			this.NormalizedExitTime = Mathf.Clamp01(profile.NormalizedExitTime);
			this._hash = profile._hash;
		}

		public override readonly string ToString() {
			return $"AnimationStateProfile(Name: {Name}, AnimationSpeed: {AnimationSpeed}, AnimationLength: {AnimationLength}, " +
				   $"IsOneShot: {IsOneShot}, NormalizedExitTime: {NormalizedExitTime},Hash: {Hash})";
		}
	}
}
