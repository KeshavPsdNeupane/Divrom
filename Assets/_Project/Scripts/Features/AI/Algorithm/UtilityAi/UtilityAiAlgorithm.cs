using System;
using System.Collections.Generic;
using Kope.AI.Utility.Config;
using UnityEngine;

namespace Kope.AI.Utility {
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

		[SerializeField, Range(0.05f, 1.0f), Tooltip("The minimum weight an action can decay to in memory." +
		" This prevents actions from becoming completely irrelevant over time.")]
		private float minActionWeight = 0.1f;

		#region Internal Classes
		protected internal class ActionEntry {
			private readonly ActionSO action;
			private float biasWeight;
			private bool isActive;
			public ActionSO Action => this.action;
			public ActionEntry(ActionSO action, float weight) {
				this.action = action;
				this.biasWeight = weight;
				this.isActive = false;
			}
			public float Evaluate(IReadOnlyContext ctx) {
				float score = this.action.Evaluate(ctx) * this.biasWeight;
				if (this.isActive) score += this.action.MomentumBias;
				// just return raw, let the x>1, so we have more dynamic range.
				// x will never be x<0, since the base evaluation will always be >= 0,
				//  and bias weight and momentum bias are also >= 0. 
				// so no need to clamp or anything. just return the raw score to allow 
				// for more dynamic range and more interesting decision making.
				return score;
			}
			public void ApplyDecay(float minWeight)
			=> this.biasWeight = Mathf.Max(minWeight, this.biasWeight * this.action.DecayRate);
			public void ResetWeight(float weight) => this.biasWeight = weight;
			public void SetIsActive(bool isActive) => this.isActive = isActive;
		}
		protected internal class Memory {
			private readonly Queue<ActionEntry> actionQueue;
			private readonly HashSet<ActionEntry> actionSet;
			private readonly int capacity;
			public int Count => this.actionQueue.Count;
			public Memory(int capacity) {
				this.actionQueue = new Queue<ActionEntry>(capacity);
				this.actionSet = new HashSet<ActionEntry>(capacity);
				this.capacity = capacity;
			}
			public bool Contains(ActionEntry action) => this.actionSet.Contains(action);
			public ActionEntry Enqueue(ActionEntry action) {
				ActionEntry removed = null;
				if (this.actionQueue.Count >= this.capacity) {
					removed = this.actionQueue.Dequeue();
					this.actionSet.Remove(removed);
				}
				this.actionQueue.Enqueue(action);
				this.actionSet.Add(action);
				return removed;
			}

			public ActionEntry Dequeue() {
				if (this.actionQueue.Count == 0) return null;
				var removed = this.actionQueue.Dequeue();
				this.actionSet.Remove(removed);
				return removed;
			}
			public void Clear() {
				this.actionQueue.Clear();
				this.actionSet.Clear();
			}
		}
		#endregion

		private ActionEntry idleActionEntry;
		private ActionEntry currentlyActiveEntry;
		private readonly List<ActionEntry> actionEntries = new();
		private Memory memory;

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
		float evaluatedScore = 0f;
		#region Initialization

		protected override bool InitializeAI() {
			if (this.idleAction == null) {
				Debug.LogError("UtilityAiAlgorithm: Idle action missing.");
				return false;
			}

			this.idleActionEntry = new ActionEntry(
				Instantiate(this.idleAction), DEFAULT_INITIAL_WEIGHT);
			this.actionEntries.Add(this.idleActionEntry);

			var actions = GetActualActionSOs();

			if (actions != null) {
				foreach (var action in actions) {
					if (action == null) continue;
					var instance = Instantiate(action);
					this.actionEntries.Add(new ActionEntry(
						instance, DEFAULT_INITIAL_WEIGHT));
				}
			}

			// Ensure memory size is at least 1 and
			//  does not exceed the number of available actions 
			// minus one (to allow for the idle action).
			var size = Mathf.Clamp(
				this.shortTermMemorySize, 1,
				Mathf.Max(1, this.actionEntries.Count - 1)
			);
			this.memory = new Memory(size);
			return true;
		}

		protected override void CleanUpAI() {
			if (this.actionEntries != null) {
				foreach (var actionEntry in this.actionEntries) {
					if (actionEntry.Action != null)
						Destroy(actionEntry.Action);
				}
				this.actionEntries.Clear();
			}
			this.memory?.Clear();
			this.memory = null;
			// No need to destroy idle action separately since it's included in actionEntries
			// just nullify the reference to allow GC to clean it up
			this.idleActionEntry = null;
		}

		#endregion


		#region Decision

		public override IEnumerable<BaseActionSO> GetDecisionPlan(IReadOnlyContext ctx) {
			yield return SelectBestAction(ctx);
		}

		private ActionSO SelectBestAction(IReadOnlyContext ctx) {
			// If there's only one action (the idle action), return it immediately without evaluation.
			// no need to waste computation on evaluating the idle action against itself,
			//  and this also prevents any potential issues with memory or biasing when 
			// only the idle action is present.
			if (this.actionEntries.Count == 1) return this.idleActionEntry.Action;

			var best = EvaluateActions(ctx);
			return ApplyMemoryPenalty(best);
		}

		private ActionEntry EvaluateActions(IReadOnlyContext ctx) {
			ActionEntry bestAction = null;
			float highestScore = float.MinValue;

			foreach (var actionEntry in this.actionEntries) {
				float score = actionEntry.Evaluate(ctx);
				//Debug.Log($"[UtilityAiAlgorithm] Evaluating action: {actionEntry.Action.name}, Score: {score}. " + GetParentGameObjectHeirarchyMessage());
				if (score > highestScore) {
					highestScore = score;
					bestAction = actionEntry;
				}
			}
			if (bestAction != this.currentlyActiveEntry) {
				this.currentlyActiveEntry?.SetIsActive(false);
				bestAction?.SetIsActive(true);
				this.currentlyActiveEntry = bestAction;
			}
			evaluatedScore = highestScore;
			return bestAction;
		}

		#endregion


		#region Memory + Recurrence
		private ActionSO ApplyMemoryPenalty(ActionEntry actionEntry) {
			if (actionEntry == null) return this.idleActionEntry.Action;

			if (this.memory.Contains(actionEntry)) {
				actionEntry.ApplyDecay(this.minActionWeight);
			} else {
				var removed = this.memory.Enqueue(actionEntry);
				if (removed != null) removed.ResetWeight(DEFAULT_INITIAL_WEIGHT);
				actionEntry.ResetWeight(DEFAULT_INITIAL_WEIGHT);
			}
			//Debug.Log($"[UtilityAiAlgorithm] Selected action: {actionEntry.Action.name} with evaluated score: {evaluatedScore}. Memory updated. Current memory count: {this.memory.Count}." + GetParentGameObjectHeirarchyMessage());
			return actionEntry.Action;
		}
		#endregion
	}
}