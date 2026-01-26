using System.Collections.Generic;
using UnityEngine;

namespace Kope.AI.Algorithm.Utility.Config
{
    public class UtilityAiConfig : ScriptableObject
    {
        [SerializeField, Tooltip("Display name of this AI logic configuration. " +
            "Use a unique name for easier debugging and identification.")]
        private string algorithmName = "Utility AI";

        [SerializeField, Tooltip("List of actions available to this Utility AI configuration.")]
        private List<ActionSO> actionSOs;
        public string AlgorithmName => algorithmName;

        public List<ActionSO> ActionSOs => actionSOs;

    }

}