using System.Collections.Generic;
using Kope.Core.EntityComponentRegistry;
using UnityEngine;

namespace Kope.AI.Utility {
	public abstract class ConsiderationSO : ScriptableObject {
		[SerializeField, Tooltip("The type of action for which this consideration is relevant." +
		"For compositie considerations, this can be set to None since the considerations within the composite handle their own action type relevance." +
		"And current Implementation of CompositeConsideration just pass the checks to inner consideration, so for composite consideration" +
		"This field is not needed.")]
		private List<ActionType> relevantActionTypes = new() { ActionType.None };
		public abstract string ConsiderationName { get; }
		private HashSet<ActionType> _relevantActionTypesSet;

		protected virtual void OnEnable() => Initialize();
		protected virtual void OnValidate() => Initialize();

		private void Initialize() {
			BuildHashSet();
			OnInitialize();
		}
		/// <summary>
		/// Hook for additional Intialization logic in derived classes.
		/// Called during OnEnable and OnValidate after building the relevant action types hash set.
		/// U can do regular Initialization logic and as well as validation of config in this method. If you find any config error, just log error and return, the consideration will simply not provide any score or target registry until the config is fixed, but it wont cause any crash or undefined behavior.
		/// This is to ensure that the consideration always has a chance to initialize its internal state and validate its configuration whenever it is enabled or modified in the inspector, without relying on the user to remember to call a separate initialization method.
		/// </summary>
		protected virtual void OnInitialize() { }

		private void BuildHashSet() {
			this._relevantActionTypesSet = new HashSet<ActionType>(this.relevantActionTypes);
		}

		public bool IsRelevantFor(ActionType actionType) {
			if (this._relevantActionTypesSet == null) BuildHashSet();

			return this._relevantActionTypesSet.Contains(ActionType.None) ||
				   this._relevantActionTypesSet.Contains(actionType);
		}


		/// <summary>
		/// Evaluates the consideration to determine its contribution to the utility score.
		/// <para>
		/// Returns a tuple containing the evaluated score and the updated total multiplication count.
		/// The multiplication count is used for compensated utility calculations to normalize the score 
		/// as more considerations are factored in.
		/// </para>
		/// <remarks>
		/// Guidelines for implementation:
		/// <list type="bullet">
		/// <item>
		/// <description>Increment <paramref name="totalMultiplicationCount"/> if this consideration returns a 
		/// normalized value (0-1) that is multiplied into the final utility score.</description>
		/// </item>
		/// <item>
		/// <description>Do NOT increment the count if the consideration returns a constant or additive value 
		/// that does not scale with the number of factors (e.g., a base priority boost).</description>
		/// </item>
		/// </list>
		/// </remarks>
		/// </summary>
		/// <param name="context">The read-only AI context containing current world and entity state.</param>
		/// <param name="totalMultiplicationCount">The current number of multiplying factors applied to the utility score.</param>
		/// <returns>A tuple where the <c>float</c> is the consideration score and the <c>int</c> is the potentially incremented multiplication count.</returns>
		public abstract (float, int) Evaluate(IReadOnlyContext context);

		/// <summary>
		/// Provides access to a selected target's component registry if this consideration 
		/// involves selecting a specific target.
		/// <para>
		/// This allows subsequent considerations or the action to access detailed information about the target
		/// without needing to re-query the context. If the consideration does not select a target, it can return null.
		/// </para>
		/// <remarks>
		/// Implement this method in considerations that identify a specific target (e.g., the closest enemy)
		/// to allow the planner to pass that target's information down the line. This is especially useful 
		/// for actions that need to interact with the target after the planner has made a decision based on it.
		/// so we dont have to do redundant queries to find the same target again in the action.
		/// </remarks>
		/// </summary>
		/// <returns></returns>
		public virtual IReadOnlyComponentRegistry GetSelectedTargetRegistry(ActionType actionType) {
			return null;
		}
	}

}