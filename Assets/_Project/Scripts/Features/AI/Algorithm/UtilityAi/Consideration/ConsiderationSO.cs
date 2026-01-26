using UnityEngine;

namespace Kope.AI.Algorithm.Utility
{
    public abstract class ConsiderationSO : ScriptableObject
    {
        public abstract string ConsiderationName { get; }
        public abstract (float, int) Evaluate(IReadOnlyEntityContext context, int considerationCount);
    }

}