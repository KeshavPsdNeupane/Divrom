using System.Collections.Generic;
using Kope.Actor.New;
using Kope.Core;
using UnityEngine;

namespace Kope.Actor {
	[System.Serializable]
	public class AnimationDataInspector : IReturn<AnimationStateProfile[]> {
		[SerializeField] private List<AnimationStateProfile> _profiles;
		public AnimationStateProfile[] GetValue() => this._profiles.ToArray();
	}
	/// <summary>
	/// A flexible data container that resolves animation profiles from either a local inspector-defined list 
	/// or a shared <see cref="AnimationDataSO"/> asset.
	/// </summary>
	/// <remarks>
	/// Returns an array of <see cref="AnimationStateProfile"/> to ensure data immutability (defensive copying) 
	/// and to optimize performance for frequent iteration during state lookups.
	/// </remarks>
	[System.Serializable]
	public class AnimationData :
		InspectorWireOrScriptableObjectConfig<AnimationDataInspector, AnimationDataSO, AnimationStateProfile[]> {
	}
}