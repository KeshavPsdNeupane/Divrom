using UnityEngine;

namespace Kope.AI.Utility
{
    public abstract class ConsiderationSO : ScriptableObject
    {
        public abstract string ConsiderationName { get; }
        public abstract (float, int) Evaluate(IReadOnlyEntityContext context, int totalMultiplicationCount);
    }

}