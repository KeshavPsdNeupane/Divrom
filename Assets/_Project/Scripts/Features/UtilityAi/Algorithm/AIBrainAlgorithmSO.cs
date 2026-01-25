using UnityEngine;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Kope.AI.Brain
{
    /// <summary>
    /// Represents a reusable AI brain algorithm for entities. <br/>
    /// 
    /// This abstract class can be implemented to create different AI types, such as: <br/>
    /// - Utility AI <br/>
    /// - GOAP (Goal-Oriented Action Planning) <br/>
    /// - Behavior Trees <br/>
    /// 
    /// The brain evaluates the current entity context and generates a **decision plan** in the form of a container of actions. <br/>
    /// The container can hold: <br/>
    /// - A single action (typical for Utility AI or Behavior Trees) <br/>
    /// - Multiple actions forming a plan (typical for GOAP or multi-step AI) <br/>
    /// 
    /// Key design principles: <br/>
    /// - **Execution order agnostic:** The brain formats the plan in the desired order (LIFO or FIFO). The executor simply follows the plan sequentially from index 0 to N. <br/>
    /// - **Separation of concerns:** Decision-making logic resides entirely in the brain, while the executor focuses solely on performing actions. <br/>
    /// - **Modularity:** Supports swapping different AI brains without changing executor or entity systems. <br/>
    /// - **Testability and maintainability:** Encapsulates AI logic in a single ScriptableObject, making unit testing, debugging, and extending behavior simpler. <br/>
    ///  <br/>
    /// Implementations must ensure that the plan is correctly ordered according to their AI logic. <br/>
    /// This allows single-step or multi-step AI systems to coexist under the same executor framework. <br/>
    /// </summary>
    public abstract class AIBrainAlgorithmSO : ScriptableObject
    {
        [SerializeField, Tooltip("The display name of this specific AI logic configuration.")]
        protected string brainName;

        public string BrainName => brainName;
        // forcing child to implement its own action storage and instantiation logic
        // so this base class remains generic for all AI types


        /// <summary>
        /// Generates a decision plan based on the provided entity context.
        /// The returned sequence of actions should be formatted by the child class
        /// according to its logic (LIFO, FIFO, or single-action plan).
        /// The executor will process the plan sequentially, following the instructions
        /// from the first element to the last, without needing to know the AI's logic.
        /// <br/>
        /// The ctx is a read-only snapshot of the entity's current state,
        /// providing all necessary information for decision-making.
        /// but does not allow modifications to the entity's state.
        /// only the brain is responsible for decision-making. 
        /// that's why it receives ctx but as read-only as IReadOnlyEntityContext.
        /// that only provides getters. not setters.
        /// </summary>
        /// <param name="ctx">
        /// </param>
        /// <returns>A sequence of actions for the executor to perform.</returns>
        public abstract IEnumerable<ActionSO> GetDecisionPlan(IReadOnlyEntityContext ctx);

    }
}
