using System.Collections.Generic;
using Kope.Actor.New;
using Kope.Core;
using UnityEngine;

namespace Kope.Feature.AnimationState {
	[System.Serializable]
	public class AnimationDataInspector : IReturn<AnimationStateProfileData[]> {
		[SerializeField] private List<AnimationStateProfile> _profiles;
		private AnimationStateProfileData[] _cachedData;
		public AnimationStateProfileData[] GetValue() {
			{
				if (this._cachedData == null || this._cachedData.Length != this._profiles.Count) {
					this._cachedData = this._profiles.ConvertAll(profile => profile.ToData()).ToArray();
				}
				return this._cachedData;

			}
		}
	}
	/// <summary>
	/// A flexible data container that resolves animation profiles from either a local inspector-defined list 
	/// or a shared <see cref="AnimationDataSO"/> asset.
	/// </summary>
	/// <remarks>
	/// Returns an array of <see cref="AnimationStateProfileData"/> to ensure data immutability (defensive copying) 
	/// and to optimize performance for frequent iteration during state lookups.
	/// </remarks>
	[System.Serializable]
	public class AnimationData :
		InspectorWireOrScriptableObjectConfig<AnimationDataInspector, AnimationDataSO, AnimationStateProfileData[]> {
	}
}