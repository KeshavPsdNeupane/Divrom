using System.Collections.Generic;
using Kope.Actor.New;
using Kope.Core;
using UnityEngine;

namespace Kope.Actor {
	[CreateAssetMenu(fileName = "AnimationDataSO", menuName = "Scriptable Objects/AnimationDataSO")]
	public class AnimationDataSO : ScriptableObject, IReturn<AnimationStateProfile[]> {
		[SerializeField] private List<AnimationStateProfile> _profiles;
		public AnimationStateProfile[] GetValue() => this._profiles.ToArray();
	}
}