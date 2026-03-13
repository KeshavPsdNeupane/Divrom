using System.Collections.Generic;
using Kope.AI.Utility.Config;
using UnityEngine;

namespace Kope.AI.Utility
{
	public class UtilityAiAlgorithm : AIBrainAlgorithm
	{
		private const float DEFAULT_INITIAL_WEIGHT = 1f;


		[Header("Default Actions")]
		[SerializeField] private ActionSO idleAction;

		[Header("Configuration")]
		[SerializeField] private UtilityAiConfig config;
		[SerializeField] private bool useConfig = true;

		[Header("Local Setup (Used if Config disabled)")]
		[SerializeField] protected string algorithmName = "Utility AI";
		[SerializeField] private List<ActionSO> actionSOs;

		[Header("Behavior Control")]
		[SerializeField] private int shortTermMemorySize = 5;

		[SerializeField, Range(0, 1)]
		private float actionRecurrencePenalty = 0.7f;

		[SerializeField, Range(0.05f, 1.0f), Tooltip("The minimum weight an action can have.")]
		private float minActionWeight = 0.1f;

		/// <summary>
		/// Stores instantiated actions and their current score weight.
		/// </summary>
		private readonly Dictionary<ActionSO, float> actionWeights = new();

		/// <summary>
		/// Memory queue of recent actions.
		/// </summary>
		private Queue<ActionSO> memoryQueue;

		/// <summary>
		/// HashSet for O(1) memory lookup.
		/// </summary>
		private HashSet<ActionSO> memorySet;

		private ActionSO idleActionInstance;

		#region Properties

		public override string AlgorithmName =>
			this.useConfig && this.config != null
				? this.config.AlgorithmName
				: this.algorithmName;

		private List<ActionSO> GetActualActionSOs() =>
			this.useConfig && this.config != null
				? this.config.ActionSOs
				: this.actionSOs;

		#endregion


		#region Initialization

		void OnDisable() => OnCleanUp();

		protected override bool InitializeAI()
		{
			if (this.idleAction == null)
			{
				Debug.LogError("UtilityAiAlgorithm: Idle action missing.");
				return false;
			}

			this.idleActionInstance = Instantiate(this.idleAction);

			this.actionWeights.Add(this.idleActionInstance, DEFAULT_INITIAL_WEIGHT);

			var actions = GetActualActionSOs();

			if (actions != null)
			{
				foreach (var action in actions)
				{
					if (action == null) continue;
					var instance = Instantiate(action);
					this.actionWeights.Add(instance, DEFAULT_INITIAL_WEIGHT);
				}
			}

			// Ensure memory size is at least 1 and
			//  does not exceed the number of available actions 
			// minus one (to allow for the idle action).
			int safeMemorySize = Mathf.Clamp(this.shortTermMemorySize, 1, Mathf.Max(1, this.actionWeights.Count - 1)
);

			this.memoryQueue = new Queue<ActionSO>(safeMemorySize);
			this.memorySet = new HashSet<ActionSO>();

			// Update the serialized field or a local variable to reflect the actual size used
			this.shortTermMemorySize = safeMemorySize;
			return true;
		}

		protected override void CleanUpAI()
		{
			foreach (var action in this.actionWeights.Keys)
			{
				if (action != null)
					Destroy(action);
			}

			this.actionWeights.Clear();

			this.memoryQueue?.Clear();
			this.memorySet?.Clear();

			this.memoryQueue = null;
			this.memorySet = null;
			this.idleActionInstance = null;
		}

		#endregion


		#region Decision

		public override IEnumerable<BaseActionSO> GetDecisionPlan(IReadOnlyContext ctx)
		{
			yield return SelectBestAction(ctx);
		}

		private ActionSO SelectBestAction(IReadOnlyContext ctx)
		{
			if (this.actionWeights.Count == 1)
				return this.idleActionInstance;

			var best = EvaluateActions(ctx);

			return ApplyMemoryPenalty(best);
		}

		private ActionSO EvaluateActions(IReadOnlyContext ctx)
		{
			ActionSO bestAction = null;
			float highestScore = float.MinValue;

			foreach (var kvp in this.actionWeights)
			{
				var action = kvp.Key;
				float weight = kvp.Value;
				float score = action.Evaluate(ctx) * weight;
				if (score > highestScore)
				{
					highestScore = score;
					bestAction = action;
				}
			}

			return bestAction;
		}

		#endregion


		#region Memory + Recurrence

		private ActionSO ApplyMemoryPenalty(ActionSO action)
		{
			// High-level fallback: If evaluation fails (null), return Idle without penalty.
			if (action == null)
			{
				return this.idleActionInstance;
			}


			if (this.memorySet.Contains(action))
			{
				// Continuous repetition: Apply the decay penalty.
				// This affects BOTH Wander and Idle now.
				float weight = this.actionWeights[action];
				weight *= this.actionRecurrencePenalty;
				this.actionWeights[action] = Mathf.Max(this.minActionWeight, weight);
			}
			else
			{
				// Transition: Managing the Short Term Memory sliding window.
				if (this.memoryQueue.Count >= this.shortTermMemorySize)
				{
					// Pop the oldest action and restore its weight to full (1.0).
					var removed = this.memoryQueue.Dequeue();
					this.memorySet.Remove(removed);
					this.actionWeights[removed] = DEFAULT_INITIAL_WEIGHT;
				}

				// Push the new action into memory.
				this.memoryQueue.Enqueue(action);
				this.memorySet.Add(action);

				// Ensure it starts at full power.
				this.actionWeights[action] = DEFAULT_INITIAL_WEIGHT;
			}
			return action;
		}
		#endregion
	}
}