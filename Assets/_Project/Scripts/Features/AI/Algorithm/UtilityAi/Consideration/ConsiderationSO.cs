using UnityEngine;

namespace Kope.AI.Utility
{
    public abstract class ConsiderationSO : ScriptableObject
    {
        public abstract string ConsiderationName { get; }
        public abstract (float, int) Evaluate(IReadOnlyContext context, int totalMultiplicationCount);
    }

}