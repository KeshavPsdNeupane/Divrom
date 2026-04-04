using System.Collections.Generic;
using Kope.AI.Utility;
using Kope.Core.Entity;
using UnityEngine;

[CreateAssetMenu(fileName = "CompositeConsideration", menuName = "Scriptable Objects/AI/Utility/Considerations/CompositeConsideration")]
public class CompositeConsideration : ConsiderationSO {
	[SerializeField] private string considerationName = "Composite Consideration";
	[SerializeField] private List<ConsiderationSO> considerations = new();
	public override string ConsiderationName => this.considerationName;

	public override (float, int) Evaluate(IReadOnlyContext context) {
		float finalScore = 1f;
		int totalMultiplicationCount = 0;
		foreach (var consideration in considerations) {
			var (score, count) = consideration.Evaluate(context);
			finalScore *= score;
			totalMultiplicationCount += count + 1; // +1 to account for this consideration's multiplication
			if (finalScore <= 0f) return (0f, totalMultiplicationCount);
		}
		return (finalScore, totalMultiplicationCount);
	}

	public override IReadOnlyComponentRegistry GetSelectedTargetRegistry(ActionType actionType) {
		foreach (var consideration in considerations) {
			var registry = consideration.GetSelectedTargetRegistry(actionType);
			if (registry != null) {
				// Return the first non-null registry found among the considerations,
				// since in a composite we will only act on one target, and the 
				// first valid one is the most relevant for the chosen action.
				// and we will never put health Range Consideration and a Attack Range Consideration
				// in the same composite, because they are used for different actions,
				// so we will never have a case where we have multiple valid registries 
				// from different considerations in the same composite.
				return registry;
			}
		}
		return null; // If no considerations provide a target registry, return null
	}


}
