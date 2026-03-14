using System;
using System.Collections.Generic;
using Kope.Core.Extensions;
using Unity.VisualScripting;
using UnityEngine;

namespace Kope.AI.Utility {

	/// <summary>
	/// Used to define an action that an AI entity can perform. <br/>
	/// Actions are evaluated based on a set of considerations to determine their utility. <br/>
	/// </summary>
	public abstract class ActionSO : BaseActionSO {

		[SerializeField] private List<ConsiderationSO> considerations;
		[SerializeField, Range(0.05f, 1.0f),
		Tooltip("This value is used to decay the bias weight of an action over time when it is not selected, " +
		"to encourage variety in action selection.")]
		private float decayRate = 0.7f;
		[SerializeField, Range(0.01f, 0.1f),
		 Tooltip("Very Small Momentum Factor Provided to AI, to encourage repetition.")]
		private float momentumBias = 0.05f;
		public float DecayRate => this.decayRate;
		public float MomentumBias => this.momentumBias;

		/// <summary>
		/// Evaluates the action's utility based on its considerations and the given context.
		/// Uses Multiplicative scoring with compensated utility.
		/// Multiplication make panalties for low scores more severe, thus promoting actions that
		/// perform well across all considerations. Compensated utility helps to balance the effect
		/// of multiple considerations to avoid overly harsh penalties for actions with many considerations.
		/// </summary>
		/// <param name="context"></param>
		/// <returns></returns>
		public float Evaluate(IReadOnlyContext context) {
			// tracks how many considerations have been multiplied together
			// to apply compensated utility correctly
			// this is needed to avoid penalizing actions with many considerations too harshly
			int totalMul = 0;
			float totalScore = 1f;
			foreach (var consideration in considerations) {
				(float score, int newCount) = consideration.Evaluate(context, totalMul);
				totalScore *= score;
				if (totalScore == 0f)
					return 0f;
				// ++ needed to account for this consideration multiplication being applied
				totalMul = ++newCount;
			}
			return totalScore.GetCompensatedUtility(totalMul);
		}
	}

}