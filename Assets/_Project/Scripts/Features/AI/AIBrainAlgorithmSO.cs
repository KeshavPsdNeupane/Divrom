using System.Collections.Generic;
using Kope.Core.Init;

namespace Kope.AI
{
    /// <summary>
    /// Base abstract class for AI decision planners.<br/>
    /// 
    /// This class defines a **pure decision planner** that can be implemented to create different AI types:<br/>
    /// - Utility AI<br/>
    /// - GOAP (Goal-Oriented Action Planning)<br/>
    /// - Behavior Trees<br/>
    /// <br/>
    /// Key principles:<br/>
    /// 1. **Pure Evaluation:** The planner is read-only and does not mutate the entity or global state.
    ///    All state changes occur during action execution.<br/>
    /// 2. **Linear Flattening:** Regardless of internal complexity (trees, graphs, GOAP sequences),
    ///    the output is always a flat, sequential <see cref="IEnumerable{BaseActionSO}"/>.<br/>
    /// 3. **Separation of Concerns:** Planning logic is contained in the planner, execution is handled by AIBrain.
    /// 4. **Modularity:** Supports swapping different planners without changing executor or entity systems.<br/>
    /// 5. **Testability:** Encapsulating the planner in a single ScriptableObject simplifies unit testing and debugging.<br/>
    /// <br/>
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
        public abstract IEnumerable<BaseActionSO> GetDecisionPlan(IReadOnlyContext ctx);

        /// <summary>
        /// Clean up any instantiated actions or internal state.
        /// so that the algorithm instance is ready for fresh planning.
        /// Must be called before generating a new plan if the algorithm
        /// maintains any internal state or instantiated actions.
        /// And Called on ONDESTROY of the  this algorithm child class .
        /// Since this is a MonoBehaviour derived class.
        /// </summary>
        public abstract void CleanUp();
    }
}
