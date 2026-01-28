using System.Collections.Generic;
using Kope.Core.Extensions;
using UnityEngine;

namespace Kope.AI.Utility
{

    /// <summary>
    /// Used to define an action that an AI entity can perform. <br/>
    /// Actions are evaluated based on a set of considerations to determine their utility. <br/>
    /// </summary>
    public abstract class ActionSO : BaseActionSO
    {

        [SerializeField] private List<ConsiderationSO> considerations;

        /// <summary>
        /// Initialize the action with the mutable context.
        /// ALways call base.Initialize(ctx) when overriding.
        /// So that action status is set to Running.
        /// This is important for IsCompleted property to work correctly.
        /// </summary>
        /// <param name="ctx"></param>


        /// <summary>
        /// Evaluates the action's utility based on its considerations and the given context.
        /// Uses Multiplicative scoring with compensated utility.
        /// Multiplication make panalties for low scores more severe, thus promoting actions that
        /// perform well across all considerations. Compensated utility helps to balance the effect
        /// of multiple considerations to avoid overly harsh penalties for actions with many considerations.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public float Evaluate(IReadOnlyEntityContext context)
        {
            // tracks how many considerations have been multiplied together
            // to apply compensated utility correctly
            // this is needed to avoid penalizing actions with many considerations too harshly
            int totalMulCount = 0;
            float totalScore = 1f;
            foreach (var consideration in considerations)
            {
                (float score, int newCount) = consideration.Evaluate(context, totalMulCount);
                totalScore *= score;
                if (totalScore == 0f)
                    return 0f;
                // ++ needed to account for this consideration multiplication being applied
                totalMulCount = ++newCount;
            }
            return totalScore.GetCompensatedUtility(totalMulCount);
        }
    }

}