using UnityEngine;
using Kope.AI.Utility;
using Kope.AI.Ctx;
[CreateAssetMenu(fileName = "ConstantConsideration", menuName = "Scriptable Objects/AI/Utility/Considerations/ConstantConsideration")]
public class ConstantConsideration : ConsiderationSO {
	[SerializeField] private string considerationName;
	[SerializeField, Min(0f)] private float constantValue = 1f;

	public override string ConsiderationName => this.considerationName;


	/// <summary>
	/// Evaluates the consideration and returns a constant score.
	/// Also returns the incremented total multiplication count.
	/// used for compensated utility calculation.
	/// 
	/// </summary>
	/// <returns></returns>
	public override (float, int) Evaluate(IReadOnlyContext context) {
		return (this.constantValue, 0); // no mult happened
	}
	public override (float, int) EvaluateNew(IReadOnlyContextNew context) {
		return (this.constantValue, 0); // no mult happened
	}

}
