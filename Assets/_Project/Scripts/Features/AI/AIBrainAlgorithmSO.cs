using UnityEngine;
using System.Collections.Generic;
using Kope.Core.Init;

namespace Kope.AI.Algorithm
{
    /// <summary>
    /// Base abstract class for AI decision planners.
    /// 
    /// This class defines a **pure decision planner** that can be implemented to create different AI types:
    /// - Utility AI
    /// - GOAP (Goal-Oriented Action Planning)
    /// - Behavior Trees
    /// 
    /// Key principles:
    /// 1. **Pure Evaluation:** The planner is read-only and does not mutate the entity or global state.
    ///    All state changes occur during action execution.
    /// 2. **Linear Flattening:** Regardless of internal complexity (trees, graphs, GOAP sequences),
    ///    the output is always a flat, sequential <see cref="IEnumerable{BaseActionSO}"/>.
    /// 3. **Separation of Concerns:** Planning logic is contained in the planner, execution is handled by AIBrain.
    /// 4. **Modularity:** Supports swapping different planners without changing executor or entity systems.
    /// 5. **Testability:** Encapsulating the planner in a single ScriptableObject simplifies unit testing and debugging.
    /// 
    /// Implementations must flatten and order their internal decision structures so that the executor can
    /// process them sequentially from 0 to N, whether it’s a single-step (Utility AI) or multi-step (GOAP) plan.
    /// </summary>
    public abstract class AIBrainAlgorithm : InitializableBase
    {
        public abstract string AlgorithmName { get; }

        /// <summary>
        /// Generates a flattened, sequential decision plan from the given entity context.
        /// 
        /// The output sequence is always linear:
        /// - Utility AI: single highest-utility action
        /// - GOAP: multi-step plan
        /// - Behavior Tree: flattened execution path
        /// 
        /// The executor (AIBrain) will process this sequence sequentially from first to last.
        /// The planner must never mutate the entity or global state; ctx is read-only.
        /// </summary>
        /// <param name="ctx">Read-only snapshot of entity state for evaluation purposes.</param>
        /// <returns>Linear sequence of <see cref="BaseActionSO"/> to execute sequentially.</returns>
        public abstract IEnumerable<BaseActionSO> GetDecisionPlan(IReadOnlyEntityContext ctx);

        /// <summary>
        /// Clean up any instantiated actions or internal state.
        /// so that the algorithm instance is ready for fresh planning.
        /// </summary>
        public abstract void CleanUp();
    }
}
