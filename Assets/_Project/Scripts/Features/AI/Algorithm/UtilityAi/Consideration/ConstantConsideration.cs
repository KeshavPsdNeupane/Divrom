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
        // no need to do ++multiplication count here since this consideration does not actually multiply the score. it just returns a constant value. so we can just return the current total multiplication count without incrementing it.
        // see CondiderationSO.Evaluate() for more details on how multiplication count is used for compensated utility calculation.
        return (this.constantValue, totalMultiplicationCount);
    }


}
