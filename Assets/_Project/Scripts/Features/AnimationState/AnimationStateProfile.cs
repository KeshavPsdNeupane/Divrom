using System;
using Kope.Actor.New;
using Kope.Core.Type.EnumAsset;
using UnityEngine;


namespace Kope.Feature.AnimationState {
	/// <summary>
	/// A serializable class representing an animation state profile for Unity's Inspector.
	/// This class is used by EnumPicker class to allow designers to configure animation states in a user-friendly way.
	/// The actual runtime data is represented by the immutable struct <see cref="AnimationStateProfileData"/>, 
	/// which is generated from this class via the <see cref="ToData"/> method.
	/// </summary>
	[Serializable]
	public class AnimationStateProfile {
		[SerializeField] private EnumPicker _animationState;
		[SerializeField] private bool _isLooping = false;
		[SerializeField] private float _animationSpeed = 1f;
		[Range(0f, 1f)][SerializeField] private float _normalizedExitTime = 0.9f;
		public EnumPicker AnimationState => this._animationState;

		public AnimationStateProfileData ToData() {
			EnumInstance instance = this._animationState.GetInstance();
			Debug.Assert(instance != null, "[AnimationStateProfile] EnumPicker has no valid selection.");
			return new AnimationStateProfileData(
				instance?.Alias ?? string.Empty,
				this._animationSpeed,
				this._isLooping,
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
		[SerializeField] private bool _isLooping = false;
		[SerializeField] private float _animationSpeed = 1f;
		[Range(0f, 1f)][SerializeField] private float _normalizedExitTime = 0.9f;

		public AnimationStateProfileData ToData(string name) {
			return new AnimationStateProfileData(
				name,
				this._animationSpeed,
				this._isLooping,
				this._normalizedExitTime
			);
		}
	}
}