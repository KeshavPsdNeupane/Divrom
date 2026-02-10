using System.Collections.Generic;
using Kope.AI.Utility;
using UnityEngine;

[CreateAssetMenu(fileName = "CompositeConsideration", menuName = "Scriptable Objects/AI/Utility/Considerations/CompositeConsideration")]
public class CompositeConsideration : ConsiderationSO
{
    [SerializeField] private string considerationName = "Composite Consideration";

    [SerializeField] private List<ConsiderationSO> considerations = new();
    public override string ConsiderationName => this.considerationName;

    public override (float, int) Evaluate(IReadOnlyContext context, int totalMultiplicationCount)
    {
        float finalScore = 1f;

        foreach (var consideration in considerations)
        {
            var (score, count) = consideration.Evaluate(context, totalMultiplicationCount);
            finalScore *= score;
            // ++ is needed to account for the current multiplication
            totalMultiplicationCount = ++count;
            if (finalScore <= 0f) return (0f, totalMultiplicationCount);
        }
        return (finalScore, totalMultiplicationCount);
    }
}
