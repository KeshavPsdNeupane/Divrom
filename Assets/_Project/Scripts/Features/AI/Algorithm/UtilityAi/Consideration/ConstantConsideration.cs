using UnityEngine;
using Kope.AI.Utility;
[CreateAssetMenu(fileName = "ConstantConsideration", menuName = "Scriptable Objects/AI/Utility/Considerations/ConstantConsideration")]
public class ConstantConsideration : ConsiderationSO
{
	[SerializeField] private string considerationName;
	[SerializeField] private float constantValue = 1f;

	public override string ConsiderationName => this.considerationName;


	/// <summary>
	/// Evaluates the consideration and returns a constant score.
	/// Also returns the incremented total multiplication count.
	/// used for compensated utility calculation.
	/// 
	/// </summary>
	/// <returns></returns>
	public override (float, int) Evaluate(IReadOnlyContext context, int totalMultiplicationCount)
	{
		// since this consideration does not perform any multiplication currently in here.
		//  we return the total multiplication count unchanged
		return (this.constantValue, totalMultiplicationCount);
	}


}
