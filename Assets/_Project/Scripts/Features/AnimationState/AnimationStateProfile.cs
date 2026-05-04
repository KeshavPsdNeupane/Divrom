using System;
using Kope.Actor.New;
using Kope.Core.Type.EnumAsset;
using UnityEngine;


namespace Kope.Feature.AnimationState {
	[Serializable]
	public class AnimationStateProfile {
		[SerializeField] private EnumPicker _animationState;
		[SerializeField] private bool _isLooping;
		[SerializeField] private float _animationSpeed;
		[Range(0f, 1f)][SerializeField] private float _normalizedExitTime;

		public AnimationStateProfileData ToData() {
			var instance = this._animationState.GetInstance();
			Debug.Assert(instance != null, "[AnimationStateProfile] EnumPicker has no valid selection.");
			return new AnimationStateProfileData(
				instance?.Alias ?? string.Empty,
				this._animationSpeed,
				this._isLooping,
				this._normalizedExitTime
			);
		}
	}
	[Serializable]
	public class AnimationStateProfileForHash {
		[SerializeField] private bool _isLooping;
		[SerializeField] private float _animationSpeed;
		[Range(0f, 1f)][SerializeField] private float _normalizedExitTime;

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