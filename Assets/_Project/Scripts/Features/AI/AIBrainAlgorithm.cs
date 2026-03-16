using System.Collections.Generic;
using Kope.Core.Init;

namespace Kope.AI {
	/// <summary>
	/// Generic base class for AI decision planners. <br/>
	/// <br/>
	/// Inherit from this class to implement specific planning algorithms<br/>
	/// such as Utility AI, GOAP, or Behavior Trees.
	/// Do not inherit directly from <see cref="AIBrainAlgorithmBase"/> unless required.<br/>
	/// <br/>
	/// This class represents a **pure decision planner**:<br/>
	/// - Evaluates decisions without mutating entity or global state<br/>
	/// - Produces a flattened, sequential <see cref="IEnumerable{BaseActionSO}"/> plan<br/>
	/// - Leaves execution and state changes to <see cref="AIBrain"/><br/>
	/// 
	/// Loop detection and mitigation are **planner-specific** and must be implemented<br/>
	/// by each concrete algorithm as needed.
	/// <br/>
	/// The generic type <typeparamref name="TAction"/> enforces type safety for
	/// idle and default actions used by the planner.<br/>
	/// </summary>

	public abstract class AIBrainAlgorithm : InitializableBase {

		public abstract string AlgorithmName { get; }
		/// <summary>
		/// Initializes the AI algorithm.
		/// Do not override this method ,overright InitializeAI instead.<br/>
		/// AND ALWAYS CALL THIS METHOD TO ENSURE PROPER INITIALIZATION.<br/>
		/// This method is used as template method for all child classes of AIBrainAlgorithm.
		/// Since we want to enforce certain checks before calling the actual initialization logic.
		/// Like checking for required assignments.
		/// This method checks for required assignments and logs errors if necessary.
		/// Always call this method to ensure proper initialization of resources.
		/// If all checks pass, it calls <see cref="InitializeAI"/> for further setup.
		/// </summary>
		protected sealed override bool OnInit() {
			OnCleanUp();
			return InitializeAI();
		}

		/// <summary>
		/// Cleans up instantiated actions and internal state.
		/// This method is used as template method for all child classes of AIBrainAlgorithm.
		/// Since we want to enforce certain cleanup steps before calling the actual cleanup logic.
		/// This method cleans up the idle action instance and then calls 
		/// <see cref="CleanUpAI"/> for further cleanup.
		/// Always call this method to ensure proper cleanup of resources.
		/// </summary>
		protected void OnCleanUp() {
			CleanUpAI();
		}

		/// <summary>
		/// Initializes the AI algorithm, setting up any necessary internal structures or state.
		/// Override this method to perform custom initialization logic.
		/// For Any child class of this AIBrainAlgorithm InilializableBase.<br/>
		/// BUT NEVER CALL THIS METHOD DIRECTLY, ALWAYS CALL <see cref="OnInit"/> INSTEAD.
		/// </summary>
		/// <returns></returns>
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
		/// Always override this method to hook into cleanup logic.
		/// Cleans up any resources or state used by the AI algorithm.
		/// Never called directly, use <see cref="OnCleanUp"/> instead.
		/// </summary>
		protected abstract void CleanUpAI();
	}
}
