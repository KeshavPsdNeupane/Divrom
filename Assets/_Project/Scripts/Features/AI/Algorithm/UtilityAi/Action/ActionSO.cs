using System.Collections.Generic;
using Kope.Core.Extensions;
using UnityEngine;

namespace Kope.AI.Algorithm.Utility
{

    /// <summary>
    /// Used to define an action that an AI entity can perform. <br/>
    /// Actions are evaluated based on a set of considerations to determine their utility. <br/>
    /// </summary>
    public abstract class ActionSO : BaseActionSO
    {

        [SerializeField] private List<ConsiderationSO> considerations;

        public float Evaluate(IReadOnlyEntityContext context)
        {
            float totalScore = 1f;

            // to track the number of considerations evaluated so we can 
            // use it to GetCompensatedUtility  after all multiplications is done to 
            // avoid bias towards lower number of considerations
            // only calculate the GetCompensatedUtility at the end once
            int considerationCount = 0;
            foreach (var consideration in considerations)
            {
                (float score, int newCount) = consideration.Evaluate(context, considerationCount);
                totalScore *= score;
                considerationCount = newCount;
                if (totalScore == 0f)
                    return 0f;
            }
            // applying compensated utility to the final score
            return totalScore.GetCompensatedUtility(considerationCount);
        }
    }

}