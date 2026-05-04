using System.Collections.Generic;
using Kope.Actor.New;
using Kope.Core;
using UnityEngine;

namespace Kope.Feature.AnimationState {
	[CreateAssetMenu(fileName = "AnimationDataSO", menuName = "Scriptable Objects/AnimationDataSO")]
	public class AnimationDataSO : ScriptableObject, IReturn<AnimationStateProfileData[]> {
		[SerializeField] private List<AnimationStateProfile> _profiles;
		private AnimationStateProfileData[] _cachedData;
		public AnimationStateProfileData[] GetValue() {
			if (this._cachedData == null || this._cachedData.Length != this._profiles.Count) {
				this._cachedData = this._profiles.ConvertAll(profile => profile.ToData()).ToArray();
			}
			return this._cachedData;
		}
		// Invalidate cache when profiles are modified in the editor, will only run in editor 
		// and not affect runtime performance
		private void OnValidate() => this._cachedData = null;
	}
}