using UnityEngine;
using Kope.AI.Algorithm.Utility;

public class ConstantConsideration : ConsiderationSO
{
    [SerializeField] private string considerationName;
    [SerializeField] private float constantValue = 1f;

    public override string ConsiderationName => this.considerationName;


    /// <summary>
    /// Evaluates the consideration and returns a constant score.
    /// Also returns the incremented consideration count.
    /// used for compensated utility calculation.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="considerationCount"></param>
    /// <returns></returns>
    public override (float, int) Evaluate(IReadOnlyEntityContext context, int considerationCount)
    {
        return (this.constantValue, ++considerationCount);
    }


}
