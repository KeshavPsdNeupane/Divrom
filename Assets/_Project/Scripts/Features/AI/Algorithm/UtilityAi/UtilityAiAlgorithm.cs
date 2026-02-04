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
        [Header("Default Actions, Must be assigned in Inspector")]
        [SerializeField] private ActionSO idleAction;


        [Header("Provide The Configuration or Do Direct Setup,\n if no configuration is provided, local setup will be used")]
        [SerializeField, Tooltip("The configuration defining the actions and parameters for this Utility AI.")]
        private UtilityAiConfig config;
        [SerializeField] private bool useConfig = true;

        [Header("If not using Config, set up directly here")]
        [SerializeField, Tooltip("Display name of this AI logic. " +
            "Use a unique name for easier debugging and identification.")]
        protected string algorithmName = "Utility AI";
        [SerializeField] private List<ActionSO> actionSOs;

        [SerializeField, Range(1, 10), Tooltip("Define after how many iteration of same action the AI should fall back to idle action as penalty" +
            "This prevents the AI from getting stuck repeating the same action indefinitely.")]
        private int maxLoopIterations = 5;


        private readonly HashSet<ActionSO> actionSOSet = new();
        private ActionSO idleActionInstance;

        private ActionSO lastSelectedAction;
        private int currentLoopCount = 0;

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
        void OnEnable() => OnInit();
        void OnDisable() => OnCleanUp();

        protected override void InitializeAI()
        {
            if (this.idleAction == null)
            {
                Debug.LogError($"UtilityAiAlgorithm Error: Idle Action is not assigned in {this.name}." +
                    $" Please assign a default Idle Action in the inspector to avoid runtime errors."
                    + GetParentGameObjectStackTraceMessage());
                return;
            }
            if (this.useConfig && this.config == null)
            {
                Debug.LogWarning("UtilityAiAlgorithm: useConfig is true but config is null. Falling back to local action list." +
                    GetParentGameObjectStackTraceMessage());
            }
            this.idleActionInstance = Instantiate(this.idleAction);
            if (this.GetActualActionSOs() == null || this.GetActualActionSOs().Count == 0)
            {
                Debug.LogWarning($"UtilityAiAlgorithm Warning: No actions assigned in {this.name}. " +
                    $"The AI will only be able to perform the idle action." +
                    GetParentGameObjectStackTraceMessage());
            }
            foreach (var action in GetActualActionSOs())
            {
                actionSOSet.Add(Instantiate(action));
            }
        }

        protected override void CleanUpAI()
        {
            if (actionSOSet != null)
            {
                foreach (var action in actionSOSet)
                {
                    if (action != null) Destroy(action);
                }
                actionSOSet.Clear();
            }
            this.idleActionInstance = null;
            this.lastSelectedAction = null;
            this.currentLoopCount = 0;
        }
        #endregion



        public override IEnumerable<BaseActionSO> GetDecisionPlan(IReadOnlyContext ctx)
        {
            // Returns only the highest-scoring action or idle action if penalized.
            yield return GetCorrectAction(ctx);
        }

        private ActionSO GetCorrectAction(IReadOnlyContext ctx)
        {
            return ValidateAndSanitizeAction(EvaluateAllActions(ctx));
        }

        private ActionSO EvaluateAllActions(IReadOnlyContext ctx)
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

        private ActionSO ValidateAndSanitizeAction(ActionSO selectedAction)
        {
            if (selectedAction == null)
            {
                this.lastSelectedAction = this.idleActionInstance;
                this.currentLoopCount = 0;
                return this.idleActionInstance;
            }
            //  Debug.Log($"<color=white><b>[AI Check]</b></color> {selectedAction.name} | Current Count: {currentLoopCount} | Max: {maxLoopIterations}");

            if (ReferenceEquals(selectedAction, this.lastSelectedAction))
            {
                if (this.currentLoopCount >= this.maxLoopIterations)
                {
                    this.currentLoopCount = 0;
                    this.lastSelectedAction = this.idleActionInstance;

                    // Debug.LogWarning($"<color=orange><b>[AI Penalty]</b></color> {selectedAction.name} hit limit! Forcing Idle.");
                    return this.idleActionInstance;
                }
                this.currentLoopCount++;
            }
            else
            {
                this.lastSelectedAction = selectedAction;
                this.currentLoopCount = 1;
            }

            return selectedAction;
        }
    }
}
