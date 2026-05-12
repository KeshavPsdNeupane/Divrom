using System;
using Kope.Core.Type.EnumAsset;
using UnityEngine;


namespace Kope.Actor.New {
	/// <summary>
	/// A serializable class representing an animation state profile for Unity's Inspector.
	/// This class is used by EnumPicker class to allow designers to configure animation states in a user-friendly way.
	/// The actual runtime data is represented by the immutable struct <see cref="AnimationStateProfileData"/>, 
	/// which is generated from this class via the <see cref="ToData"/> method.
	/// </summary>
	[Serializable]
	public class AnimationStateProfile {
		[SerializeField] private EnumPicker _animationState;
		[SerializeField, Min(0f), Tooltip("The total duration of the animation in seconds, matching the Animator's 'Seconds.Milliseconds' format.\n\n" +
			"Direct Conversion Guide:\n" +
			"- If Animator shows 0:45, enter 0.45\n" +
			"- If Animator shows 1:20, enter 1.20\n" +
			"- If Animator shows 1:55, enter 1.55")]
		private float _durationInSeconds = 0f;
		[SerializeField, Min(0.1f), Tooltip("The speed at which the animation plays. 1.0 is" +
		" normal speed, <1 is slower, >1 is faster.")]
		private float _animationSpeed = 1f;
		[SerializeField, Tooltip("When enabled (One-Shot), the State Machine treats this animation as" +
		" a non-interruptible sequence that must reach its 'Normalized Exit Time' " +
		"before allowing external transitions. \n\n" +
		"When disabled (Loop), the animation is considered interruptible at any frame.")]
		private bool _isOneShot = true;
		[Range(0f, 1f)][SerializeField] private float _normalizedExitTime = 0.9f;

		public EnumPicker StatePicker => this._animationState;

		public AnimationStateProfileData ToData() {
			EnumInstance instance = this._animationState.GetInstance();
			Debug.Assert(instance != null, "[AnimationStateProfile] EnumPicker has no valid selection.");
			return new AnimationStateProfileData(
				instance?.Alias ?? string.Empty,
				this._durationInSeconds,
				this._animationSpeed,
				this._isOneShot,
				this._normalizedExitTime
			);
		}
	}

	/// <summary>
	/// A serializable class representing an animation state profile for Unity's 
	/// Inspector, specifically designed for use in EnumTable lookups.
	/// This class is used by EnumTable to allow designers to configure 
	/// animation states in a user-friendly way, where the Enum's Alias is 
	/// used as the state name in the Animator Controller.
	/// The actual runtime data is represented by the immutable struct 
	/// <see cref="AnimationStateProfileData"/>, 
	/// </summary>
	[Serializable]
	public class AnimationStateMappedProfile {
		[SerializeField, Min(0f), Tooltip("The total duration of the animation in seconds, matching the Animator's 'Seconds.Milliseconds' format.\n\n" +
		"Direct Conversion Guide:\n" +
		"- If Animator shows 0:45, enter 0.45\n" +
		"- If Animator shows 1:20, enter 1.20\n" +
		"- If Animator shows 1:55, enter 1.55")]
		private float _durationInSeconds = 0f;
		// min 0.1 to avoid zero or negative speeds which can cause issues with Animator playback.
		[SerializeField, Min(0.1f)] private float _animationSpeed = 1f;
		[SerializeField, Tooltip("When enabled (One-Shot), the State Machine treats this animation as" +
		" a non-interruptible sequence that must reach its 'Normalized Exit Time' " +
		"before allowing external transitions. \n\n" +
		"When disabled (Loop), the animation is considered interruptible at any frame.")]
		private bool _isOneShot = true;
		[Range(0f, 1f)][SerializeField] private float _normalizedExitTime = 0.9f;

		public AnimationStateProfileData ToData(string name) {
			return new AnimationStateProfileData(
				name,
				this._durationInSeconds,
				this._animationSpeed,
				this._isOneShot,
				this._normalizedExitTime
			);
		}
	}
}