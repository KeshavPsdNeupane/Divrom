using System.Collections.Generic;
using Kope.AI.Utility.Config;
using UnityEngine;


namespace Kope.AI.Utility
{
    /// <summary>
    /// Utility AI algorithm implementation for entities.
    /// 
    /// This ScriptableObject is intended to be instantiated into multiple distinct assets for different AI types,
    /// depending on the entity using it. Examples:
    /// - Melee Enemy Utility AI
    /// - Ranged Enemy Utility AI
    /// - NPC Utility AI
    /// 
    /// Each asset can define its own set of actions. If an entity type lacks a specific action
    /// (e.g., a ranged attack for a melee-only enemy), the AI will not attempt it, making this design safe and flexible.
    /// 
    /// Key design principles:
    /// 1. **Per-Entity Action Instantiation:** All actions in `actionSOs` are instantiated per entity,
    ///    ensuring that mutable action state is isolated and avoiding shared-state conflicts.
    /// 2. **Highest-Utility Selection:** This AI evaluates all actions and returns only the highest scoring 
    ///    action as a linear decision plan, which the executor then performs.
    /// 3. **Flexible AI Logic:** Different assets allow tailoring the AI logic to only include relevant actions.
    /// 4. **Debugging & Identification:** Algorithm names can be shared, but unique names are recommended
    ///    for easier debugging and identification.
    /// 
    /// By enforcing per-entity instantiation and selecting only valid actions, this algorithm ensures correct
    /// behavior without additional runtime checks.
    /// </summary>
    public class UtilityAiAlgorithm : AIBrainAlgorithm
    {
        [Header("Provide The Configuration or Do Direct Setup,\n if no configuration is provided, local setup will be used")]
        [SerializeField, Tooltip("The configuration defining the actions and parameters for this Utility AI.")]
        private UtilityAiConfig config;
        [SerializeField] private bool useConfig = true;

        [Header("If not using Config, set up directly here")]
        [SerializeField, Tooltip("Display name of this AI logic. " +
            "Use a unique name for easier debugging and identification.")]
        protected string algorithmName = "Utility AI";

        [SerializeField] private List<ActionSO> actionSOs;

        private readonly HashSet<ActionSO> actionSOSet = new();


        #region  Internal or Exposed Properties
        public override string AlgorithmName => this.useConfig && this.config != null
            ? this.config.AlgorithmName
            : this.algorithmName;

        private List<ActionSO> GetActualActionSOs() => this.useConfig && this.config != null
            ? this.config.ActionSOs
            : this.actionSOs;
        public List<ActionSO> ActionSOs => GetActualActionSOs();
        #endregion 


        #region Unity Callbacks For Safeguarding Action Set
        void OnEnable() => InitializeActionSet();
        void OnDisable() => CleanUp();
        void OnDestroy() => CleanUp();
        public override void CleanUp()
        {
            if (actionSOSet != null)
            {
                foreach (var action in actionSOSet)
                {
                    if (action != null) Destroy(action);
                }
                actionSOSet.Clear();
            }
        }

        private void InitializeActionSet()
        {
            if (useConfig && config == null)
            {
                Debug.LogWarning("UtilityAiAlgorithm: useConfig is true but config is null. Falling back to local action list.");
            }

            CleanUp();

            foreach (var action in GetActualActionSOs())
            {
                // Instantiate per entity to ensure independent mutable state
                actionSOSet?.Add(Instantiate(action));
            }
        }

        #endregion

        public override void Init()
        {
            if (this.IsInitialized) return;
            base.Init();
            InitializeActionSet();
            Debug.Log("Actions initialized for Utility AI Algorithm: " + AlgorithmName +
                " with " + actionSOSet.Count + " actions. with names" + string.Join(", ",
                System.Linq.Enumerable.Select(actionSOSet, a => a.ActionName)));
        }

        public override IEnumerable<BaseActionSO> GetDecisionPlan(IReadOnlyEntityContext ctx)
        {
            // Returns only the highest-scoring action
            yield return GetHighestScoringAction(ctx);
        }

        private ActionSO GetHighestScoringAction(IReadOnlyEntityContext ctx)
        {
            ActionSO bestAction = null;
            float highestScore = float.MinValue;

            foreach (var action in actionSOSet)
            {
                float score = action.Evaluate(ctx);
                if (score > highestScore)
                {
                    highestScore = score;
                    bestAction = action;
                }
            }

            return bestAction;
        }


    }
}
