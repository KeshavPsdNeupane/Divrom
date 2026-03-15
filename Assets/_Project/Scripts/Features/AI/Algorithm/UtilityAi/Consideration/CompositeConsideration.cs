using System.Collections.Generic;
using Kope.AI.Utility;
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
}
