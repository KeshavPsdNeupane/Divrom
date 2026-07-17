using System.Collections.Generic;
using Kope.Core.EntityComponentRegistry;
using Kope.Core.Collections.Extensions;
using UnityEngine;
using Kope.AI.AIBlackBoard;

namespace Kope.AI.Utility {

	/// <summary>
	/// Used to define an action that an AI entity can perform. <br/>
	/// Actions are evaluated based on a set of considerations to determine their utility. <br/>
	/// </summary>
	public abstract class ActionSO : BaseActionSO {
		[SerializeField, Tooltip("The type of action this represents.")]
		protected ActionType actionType = ActionType.None;
		[SerializeField] private List<ConsiderationSO> considerations;

		[SerializeField, Range(0.05f, 1.0f),
		Tooltip("This value is used to decay the bias weight of an action over time when it is not selected, " +
		"to encourage variety in action selection.")]
		private float decayRate = 0.7f;

		[SerializeField, Range(0.0f, 1.0f),
		Tooltip("If the current active action is not this action and " +
		"this action is in memory, this rate will try to regenerate the bias weight of this action to make it more likely to be selected again in the future.")]
		private float weightRegenRate = 0.20f;

		[SerializeField, Range(0.01f, 0.1f),
		Tooltip("Very Small Momentum Factor Provided to AI, to encourage repetition.")]
		private float momentumBias = 0.05f;

		public ActionType ActionType => this.actionType;
		public float DecayRate => this.decayRate;
		public float MomentumBias => this.momentumBias;
		public float WeightRegenRate => this.weightRegenRate;

		public virtual float EvaluateNew(IReadOnlyContext context) {
			// marking virtual so we can add logging or other custom behavior in specific actions if needed without affecting the base evaluation logic

			// tracks how many considerations have been multiplied together
			// to apply compensated utility correctly
			// this is needed to avoid penalizing actions with many considerations too harshly
			int totalMul = 0;
			float totalScore = 1f;
			foreach (var consideration in considerations) {
				(float score, int newCount) = consideration.EvaluateNew(context);
				totalScore *= score;
				if (totalScore == 0f) return 0f;

				totalMul += newCount + 1; // the +1 is for the current consideration's multiplication
			}
			return Mathf.Max(totalScore.GetCompensatedUtility(totalMul), 0.0f);
		}

		protected IReadOnlyComponentRegistry GetSelectedTargetRegistry(ActionType actionType) {
			foreach (var consideration in considerations) {
				var registry = consideration.GetSelectedTargetRegistry(actionType);
				if (registry != null) return registry;
			}
			return null;
		}
	}

}