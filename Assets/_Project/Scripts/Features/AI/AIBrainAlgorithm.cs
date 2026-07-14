using System.Collections.Generic;
using Kope.EntityComponentSystem;

namespace Kope.AI {
	/// <summary>
	/// Generic base class for AI decision planners. <br/>
	/// <br/>
	/// Inherit from this class to implement specific planning algorithms<br/>
	/// such as Utility AI, GOAP, or Behavior Trees.<br/>
	/// <br/>
	/// This class represents a **pure decision planner**:<br/>
	/// - Evaluates decisions without mutating entity or global state<br/>
	/// - Produces a flattened, sequential <see cref="IEnumerable{BaseActionSO}"/> plan<br/>
	/// - Leaves execution and state changes to <see cref="AIBrain"/><br/>
	/// 
	/// Loop detection and mitigation are **planner-specific** and must be implemented<br/>
	/// by each concrete algorithm as needed.
	/// <br/>
	/// </summary>

	public abstract class AIBrainAlgorithm : ComponentBase {

		public abstract string AlgorithmName { get; }

		/// <summary>
		/// Template method that enforces pre-initialization cleanup before delegating to <see cref="InitializeAI"/>.
		/// Do not override this method; override <see cref="InitializeAI"/> instead.
		/// </summary>
		protected sealed override bool OnInit() {
			OnCleanUp();
			return InitializeAI();
		}

		/// <summary>
		/// Template method that delegates to <see cref="CleanUpAI"/>.
		/// Call this to trigger cleanup; do not call <see cref="CleanUpAI"/> directly.
		/// </summary>
		protected void OnCleanUp() {
			CleanUpAI();
		}

		/// <summary>
		/// Override this to implement algorithm-specific initialization logic.
		/// Called by <see cref="OnInit"/> after pre-initialization cleanup.
		/// </summary>
		/// <returns>True if initialization succeeded; false otherwise.</returns>
		protected abstract bool InitializeAI();

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
		/// Override this to release any resources or state owned by the algorithm.
		/// Called by <see cref="OnCleanUp"/>; do not invoke directly.
		/// </summary>
		protected abstract void CleanUpAI();
	}
}
