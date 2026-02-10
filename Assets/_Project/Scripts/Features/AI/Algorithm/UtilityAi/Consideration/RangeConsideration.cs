using UnityEngine;
using Kope.AI.Utility;

[CreateAssetMenu(fileName = "RangeConsideration", menuName = "Scriptable Objects/AI/UtilityAi/Considerations/RangeConsideration")]
public class RangeConsideration : ConsiderationSO
{
    [SerializeField] private string considerationName = "Range Consideration";
    [SerializeField] private float maxRange = 10f;
    [SerializeField] private AnimationCurve rangeCurve = AnimationCurve.EaseInOut(0, 1, 10, 0);
    public override string ConsiderationName => this.considerationName;

    public override (float, int) Evaluate(IReadOnlyContext context, int totalMultiplicationCount)
    {
        var dump = this.maxRange;
        // same as constantConsideration we donot need to do 
        // ++totalMultiplicationCount here since we are not multiplying the score with anything.
        //  we are directly returning the curve evaluation result based on the distance to target,
        //  so there is no multiplication happening in this consideration. 
        // the totalMultiplicationCount is only relevant for considerations that multiply 
        // their score with the current total score, which is not the case for this RangeConsideration
        //  since it returns a score based on an evaluation of the distance to target against 
        // the max range using the provided animation curve, without multiplying it with any existing score.
        //  so we can safely ignore the totalMultiplicationCount in this consideration and just return 
        // the evaluated score from the rangeCurve based on the distance to target.
        return (1.0f, totalMultiplicationCount);
    }


}
