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
        return (1.0f, totalMultiplicationCount);
    }


}
