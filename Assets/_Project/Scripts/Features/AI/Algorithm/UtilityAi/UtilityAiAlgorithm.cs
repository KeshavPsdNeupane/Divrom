// Copyright (c) 2026 Keshav Prasad Neupane (Kope)
// Licensed under the MIT License. See LICENSE in the repository root for details.
using System;
using System.Collections.Generic;
using Kope.AI.Utility.Config;
using ThirdParty.PriorityQueeu;
using UnityEngine;

namespace Kope.AI.Utility {
	/// <summary>
	/// A Utility AI implementation that selects actions based on a combination of their evaluated 
	/// scores and a dynamic bias weight. The algorithm maintains a short-term memory of recently executed actions to encourage
	/// variety in behavior and prevent the same actions from being chosen repeatedly. Actions that are 
	/// selected will have their bias weight decayed, making them less likely to be chosen again
	/// in the near future, while non-selected actions will gradually regenerate their bias weight over time.
	/// Highly optimized for performance with a focus on minimizing allocations and ensuring fast evaluations,
	/// making it suitable for real-time decision-making in games.<br/>
	/// This class is never called every frame. It is only called when the AI needs to make a decision,
	/// which is determined by the AIBrain component's decision frequency.
	/// The decision frequency can be configured to balance between responsiveness and performance, 
	/// allowing for more complex evaluations without impacting frame rate.
	/// so we had to use Time.time and Time.deltaTime to ensure that the decay and regeneration of 
	/// action weights are consistent regardless of the decision frequency.
	/// </summary>
	public class UtilityAiAlgorithm : AIBrainAlgorithm {
		private const float DEFAULT_INITIAL_WEIGHT = 1f;

		[Header("Default Actions")]
		[SerializeField] private ActionSO idleAction;

		[Header("Configuration")]
		[SerializeField] private UtilityAiConfig config;
		[SerializeField] private bool useConfig = true;

		[Header("Local Setup (Used if Config disabled)")]
		[SerializeField] protected string algorithmName = "Utility AI";
		[SerializeField] private List<ActionSO> actionSOs;

		[Header("Behavior Control"), Range(1, 20)]
		[SerializeField] private int shortTermMemorySize = 5;

		[SerializeField, Range(0.05f, 1.0f), Tooltip("The minimum weight an action can decay to.")]
		private float minActionWeight = 0.1f;

		[SerializeField, Range(1.0f, 10.0f), Tooltip("Multiplier for time elapsed to control the speed of weight regeneration using compound interest formula.")]
		private float timeElapsedMult = 2.0f;

		#region Internal Classes
		protected internal class ActionEntry : IHasCost<float> {
			private readonly ActionSO action;
			private float biasWeight;
			private bool isActive;


			#region Debug
			private float _lastRawScore; // Store the last raw score for debugging/visualization
			private float _evaluatedScore; // Store the last evaluated score for debugging/visualization
			public float LastRawScore => _lastRawScore;
			public float EvaluatedScore => _evaluatedScore;
			public float BiasWeight => biasWeight;
			public ActionSO Action => this.action;
			#endregion

			public ActionEntry(ActionSO action, float weight) {
				this.action = action;
				this.biasWeight = weight;
				this.isActive = false;
			}

			public float GetCost() => this.biasWeight;

			public float Evaluate(IReadOnlyContext ctx) {
				float rawScore = this.action.Evaluate(ctx);
				float score = rawScore * this.biasWeight;
				if (this.isActive) score += this.action.MomentumBias;
				this._lastRawScore = rawScore;
				this._evaluatedScore = score;
				return score;
			}

			public void ApplyDecay(float minWeight)
				=> this.biasWeight = Mathf.Max(minWeight, this.biasWeight * this.action.DecayRate);

			public void ResetWeight(float weight) => this.biasWeight = weight;

			public void RegenWeights(float interval) {
				if (this.isActive || interval <= 0) return;
				// Compound interest recovery
				// float compoundedRegenAmount = this.biasWeight * (Mathf.Pow(1.0f + this.action.WeightRegenRate, interval) - 1.0f);
				// this.biasWeight = Mathf.Min(1f, this.biasWeight + compoundedRegenAmount);
				// below is the optimized version of the above formula, which is mathematically equivalent but with fewer operations.
				// derivation: let r = this.action.WeightRegenRate, w = this.biasWeight, t = interval
				// b1 =b0 + b0 * ((1 + r)^t -1) , opening the bracket
				//    =b0 + b0 * (1 + r)^t - b0 , the b0 and -b0 cancel out
				//    = b0 * (1 + r)^t, now this formula is always greater than 0,so no need to use max function 
				// to clamp the minimum value, and we can directly use min function to clamp the maximum value.
				this.biasWeight = Mathf.Min(1f, this.biasWeight * Mathf.Pow(1.0f + this.action.WeightRegenRate, interval));
			}

			public void SetIsActive(bool isActive) {
				this.isActive = isActive;
			}
		}

		protected internal class Memory {
			private readonly PriorityQueueSimple<ActionEntry, float> actionQueue;

			private readonly int memoryCapacity;
			public int Count => this.actionQueue.Count;

			public Memory(int capacity) {
				this.actionQueue = new PriorityQueueSimple<ActionEntry, float>(capacity);
				this.memoryCapacity = capacity;
			}

			public bool Contains(ActionEntry action) => this.actionQueue.Contains(action);

			public ActionEntry Enqueue(ActionEntry action) {
				ActionEntry removed = null;
				if (this.actionQueue.Count >= this.memoryCapacity) {
					// it is garunteed that the queue is full, so Dequeue will always return an entry.
					removed = this.actionQueue.Dequeue();
				}
				this.actionQueue.EnqueueOrUpdate(action);
				return removed;
			}

			public ActionEntry Dequeue() {
				if (this.actionQueue.Count == 0) return null;
				var removed = this.actionQueue.Dequeue();
				return removed;
			}

			public void DecayWeight(ActionEntry entry, float minWeight) {
				if (!Contains(entry)) return;
				entry.ApplyDecay(minWeight);
			}
			public void UpdatePriority(ActionEntry entry) {
				if (!Contains(entry)) return;
				this.actionQueue.TryUpdatePriority(entry);
			}

			public void RegenWeights(ActionEntry except, float lastTime, float currentTime, float timeElapsedMult) {
				// elapsed time is in seconds, so we can use it directly with the compound interest formula in ActionEntry.RegenWeights.
				// and using pow function will not be expensive since elapsed time is small and
				// we are not doing it every frame, but only when the AI needs to make a decision.
				float elapsed = (currentTime - lastTime) * timeElapsedMult;
				if (elapsed <= 0) return;

				var entries = this.actionQueue.GetElements();
				foreach (var entry in entries) {
					if (entry == except) continue;
					entry.RegenWeights(elapsed);
					this.actionQueue.TryUpdatePriority(entry);
				}
			}
			public void Clear() {
				this.actionQueue.Clear();
			}
		}
		#endregion

		private ActionEntry idleActionEntry;
		private ActionEntry currentlyActiveEntry;
		private readonly List<ActionEntry> actionEntries = new();
		private Memory memory;
		private float lastEvaluationTime = 0f;

		public override string AlgorithmName => this.useConfig && this.config != null ? this.config.AlgorithmName : this.algorithmName;

		#region Initialization
		protected override bool InitializeAI() {
			if (this.idleAction == null) return false;

			this.idleActionEntry = new ActionEntry(Instantiate(this.idleAction), DEFAULT_INITIAL_WEIGHT);
			this.actionEntries.Add(this.idleActionEntry);

			var actions = this.useConfig && this.config != null ? this.config.ActionSOs : this.actionSOs;
			if (actions != null) {
				foreach (var action in actions) {
					if (action == null) continue;
					this.actionEntries.Add(new ActionEntry(Instantiate(action), DEFAULT_INITIAL_WEIGHT));
				}
			}
			// Ensure short-term memory size is at least 1 and less than the total number of actions to allow for meaningful memory management.
			// so this ensure the action is from 1 to n-1, where n is the total number of actions including idle.
			int size = Mathf.Clamp(this.shortTermMemorySize, 1, Mathf.Max(1, this.actionEntries.Count - 1));
			this.memory = new Memory(size);
			return true;
		}

		protected override void CleanUpAI() {
			foreach (var entry in this.actionEntries) if (entry.Action != null) Destroy(entry.Action);
			this.actionEntries.Clear();
			this.memory?.Clear();
			this.memory = null;
		}
		#endregion

		public override IEnumerable<BaseActionSO> GetDecisionPlan(IReadOnlyContext ctx) {
			yield return SelectBestAction(ctx);
		}

		private ActionSO SelectBestAction(IReadOnlyContext ctx) {
			// Optimization: If there's only one action (the idle action),
			// skip evaluation and return it immediately.
			const int IDLE_ONLY_COUNT = 1;
			if (this.actionEntries.Count == IDLE_ONLY_COUNT) return this.idleActionEntry.Action;

			// Regenerate weights for all non-active actions based on the time elapsed since the last evaluation.
			// using Compound interest formula for more dynamic recovery: newWeight = currentWeight + (currentWeight * (regenRate * ticks))
			this.memory.RegenWeights(this.currentlyActiveEntry, this.lastEvaluationTime, Time.time, this.timeElapsedMult);

			var best = EvaluateActions(ctx);
			return RunMemoryTask(best);
		}

		private ActionEntry EvaluateActions(IReadOnlyContext ctx) {
			ActionEntry bestAction = null;
			float highestScore = float.MinValue;
			//			Debug.Log("Evaluating Actions:");
			foreach (var entry in this.actionEntries) {
				float score = entry.Evaluate(ctx);
				//Debug.Log($"[UtilityAI] Evaluating the action named {entry.Action.name} with the score = {score} and bias = {entry.BiasWeight}");
				if (score > highestScore) {
					highestScore = score;
					bestAction = entry;
				}
			}

			if (bestAction != this.currentlyActiveEntry) {
				this.currentlyActiveEntry?.SetIsActive(false);
				bestAction?.SetIsActive(true);
				this.currentlyActiveEntry = bestAction;
			}
			return bestAction;
		}

		private ActionSO RunMemoryTask(ActionEntry actionEntry) {
			// BUG FIX: Ensure actionEntry isn't null for the logic below
			actionEntry ??= this.idleActionEntry;

			if (actionEntry == this.idleActionEntry) {
				var rescued = this.memory.Dequeue();
				if (rescued != null) {
					rescued.ResetWeight(DEFAULT_INITIAL_WEIGHT);
					this.memory.Enqueue(rescued);
					// the above Enqueue will never evict any other action.
					// since we removed 1 action and added it back, the memory is effectively unchanged,
					// but we get to reset the weight of the rescued action.
				}
			} else if (this.memory.Contains(actionEntry)) {
				this.memory.DecayWeight(actionEntry, this.minActionWeight); // apply decay
				this.memory.UpdatePriority(actionEntry); // sync
			} else {
				var removed = this.memory.Enqueue(actionEntry);
				// no need to update priority for removed there, since they 
				// are not in memory
				removed?.ResetWeight(DEFAULT_INITIAL_WEIGHT);
				actionEntry.ResetWeight(DEFAULT_INITIAL_WEIGHT); ;
				//Apply Decay and update 
				this.memory.DecayWeight(actionEntry, this.minActionWeight);
				this.memory.UpdatePriority(actionEntry);
			}

			this.lastEvaluationTime = Time.time;
			return actionEntry.Action;
		}

#if UNITY_EDITOR
		void OnDrawGizmos() {
			if (!Application.isPlaying || this.currentlyActiveEntry == null) return;
			// Setup a clean, readable style
			GUIStyle style = new();
			style.normal.textColor = Color.black;
			style.fontSize = 20;
			style.fontStyle = FontStyle.Bold;
			style.alignment = TextAnchor.UpperCenter;

			string currentActionName = this.currentlyActiveEntry.Action != null
			   ? this.currentlyActiveEntry.Action.name
			   : "None";

			// Draw only the requested info
			string labelText = $"{currentActionName}\n(Raw: {this.currentlyActiveEntry.LastRawScore:F2}\n" +
			$" bias: {this.currentlyActiveEntry.BiasWeight:F2} \n Eval: {this.currentlyActiveEntry.EvaluatedScore:F2})";

			UnityEditor.Handles.Label(transform.position + Vector3.up * 2.0f, labelText, style);
		}
#endif

	}
}