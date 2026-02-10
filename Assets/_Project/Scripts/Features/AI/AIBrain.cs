using System.Collections;
using System.Collections.Generic;
using Kope.Core.Init;
using Kope.Component.Interfaces;
using UnityEngine;
using ThirdParty;
using System;
using Kope.Core.EntityComponentSystem;
using System.Linq;

namespace Kope.AI
{
    /// <summary>
    /// The AI brain component responsible for decision-making.
    /// It utilizes a specified AI brain algorithm to generate action plans
    /// based on the entity's current context.
    /// </summary>
    public class AIBrain : InitializableBase
    {
        #region Inspector Fields
        [SerializeField] private EntityComponentStore ecs;

        [SerializeField, Tooltip("The AI brain algorithm that defines the decision-making logic.")]
        private AIBrainAlgorithm planner;

        [SerializeField, Tooltip("Components used for context. Only those implementing IInterruptOther will be subscribed.")]
        private List<InitializableBase> components;

        [SerializeField, Range(0f, 20f), Tooltip("Interval to force the brain to refresh its plan periodically. Set to 0 to disable.")]
        private float refreshInterval = 1.0f;
        #endregion

        #region Private Fields
        private Context ctx;
        private EntityStateController entityStateController;
        private BaseActionSO currentAction;
        private Coroutine actionRoutine;
        private Coroutine executionRoutine;
        private IEnumerator<BaseActionSO> currentPlanEnumerator;

        private CountdownTimer refreshTimer;
        private readonly List<IInterruptOther> interrupters = new();
        #endregion

        public override void OnInit()
        {
            base.OnInit();

            if (this.ecs == null || this.planner == null)
            {
                Debug.LogError($"AIBrain Error: Missing ECS or Planner on {gameObject.name}");
                return;
            }

            if (!this.ecs.ComponentRegistry.TryGetComponent(out this.entityStateController))
            {
                Debug.LogError($"AIBrain Error: EntityStateController not found on {gameObject.name}");
                return;
            }

            this.ctx = new Context(this.ecs.ComponentRegistry);

            // Register components and setup interrupts
            foreach (var comp in components)
            {
                this.ctx.CurrentMutableEntityContext.AddComponent(comp);
                if (comp is IInterruptOther interrupter)
                {
                    interrupter.OnInterruptRequested -= HandleInterruptSignal;
                    interrupter.OnInterruptRequested += HandleInterruptSignal;
                    this.interrupters.Add(interrupter);
                }
            }

            this.planner.OnInit();

            // Setup Timer
            if (this.refreshInterval > 0f)
            {
                this.refreshTimer = new CountdownTimer(this.refreshInterval);
                this.refreshTimer.OnTimerStop += RefreshTimerCallback;
                this.refreshTimer.Start();
            }
        }

        private void OnDestroy()
        {
            foreach (var interrupter in this.interrupters)
                interrupter.OnInterruptRequested -= HandleInterruptSignal;
        }





        /// <summary>
        /// graph TD
        ///     A[Update Start] --> B{Can State Machine Accept Command?}
        ///     B -- No --> C[Stop Current Action & Exit]
        ///     B -- Yes --> D{Is Current Action Running?}

        ///     D -- Yes --> E{Is Action Completed?}
        ///     E -- No --> F[Exit: Let Action Finish]
        ///     E -- Yes --> G[Fetch New Plan]

        ///     D -- No --> G

        ///     G --> H{New Action == Current Action?}
        ///     H -- Yes --> I[Exit: Continue Current Action]
        ///     H -- No --> J[Execute New Action]

        ///     J --> K[Start Coroutine for Action Execution]
        ///     K --> L[Action Executes Until Completed]
        ///     L --> M[Action Ends or Aborted Cleanup]
        /// Explanation of Flow:
        /// Update Start → Brain tick begins.
        /// 
        /// State Machine Check → If the entity is busy/stunned, stop current action.
        /// Action Running Check → If an action is still executing, wait until it completes.
        /// Fetch Plan → Ask the planner for the next action if the current action is done or null.
        /// Guard → If the new plan’s first action is the same as current, keep running; else, execute it.
        /// Execute New Action → Run the action via coroutine.
        /// End/Abort Cleanup → After action finishes, reset state and prepare for next update.
        /// </summary>
        protected override void OnUpdate()
        {
            base.OnUpdate();
            if (this.planner == null || this.ecs == null) return;

            // Tick timer
            refreshTimer?.Tick(Time.deltaTime);

            // Busy Check
            if (!this.entityStateController.CanStateMachineAcceptCommand)
            {
                if (this.currentAction != null) StopCurrentAction();
                return;
            }

            // Execute plan if current action is done
            if (this.currentAction == null || this.currentAction.IsCompleted)
            {
                if (this.currentPlanEnumerator == null) FetchNewPlan();
                ExecuteThePlan();
            }
        }

        protected virtual void FetchNewPlan()
        {
            // 1. Get the raw plan (IEnumerable)
            var newPlan = this.planner.GetDecisionPlan(this.ctx);
            if (newPlan == null) return;

            // 2. Materialize into a list immediately to save the result
            var planList = newPlan.ToList();
            if (planList.Count == 0) return;

            // 3. Peek at the first action for the Guard check
            var topAction = planList[0];

            // 4. If the new best action is what we are already doing, 
            // simply bail and let the current action keep running.
            if (ReferenceEquals(this.currentAction, topAction)) return;

            // 5. If it's a new action, set up the enumerator for ExecuteThePlan()
            this.currentPlanEnumerator = planList.GetEnumerator();
            this.currentAction = null;
        }

        protected virtual void ExecuteThePlan()
        {
            if (this.currentAction != null && !this.currentAction.IsCompleted) return;

            if (this.currentPlanEnumerator != null && this.currentPlanEnumerator.MoveNext())
            {
                ExecuteNewAction(this.currentPlanEnumerator.Current);
            }
            else
            {
                this.currentPlanEnumerator = null;
                this.currentAction = null;
            }
        }

        protected virtual void ExecuteNewAction(BaseActionSO action)
        {
            if (action == null) return;

            this.currentAction = action;
            this.actionRoutine = StartCoroutine(RunActionSequence(action));
        }

        protected virtual IEnumerator RunActionSequence(BaseActionSO action)
        {
            action.Initialize(this.ctx.CurrentMutableEntityContext);
            bool actionFinished = false;

            void handler() => actionFinished = true;

            action.OnActionCompleted += handler;

            try
            {
                this.executionRoutine = StartCoroutine(action.Execute(this.ctx));
                yield return this.executionRoutine;
                yield return new WaitUntil(() => actionFinished || action.IsCompleted);
                yield return new WaitForEndOfFrame();
            }
            finally
            {
                action.OnActionCompleted -= handler;
//                Debug.Log($"<color=green><b>[AI Action]</b></color> {action.name} completed on {gameObject.name}");
                if (this.currentAction == action)
                {
                    action.EndOrAbort(this.ctx.CurrentMutableEntityContext);
                    this.currentAction = null;
                    this.actionRoutine = null;
                    this.executionRoutine = null;
                }
            }
        }

        public virtual void ForceInterrupt(InterruptPriority priority = InterruptPriority.Soft)
        {
            switch (priority)
            {
                case InterruptPriority.Soft:
                    this.currentPlanEnumerator = null;
                    if (this.currentAction != null && this.currentAction.IsInterruptible)
                        StopCurrentAction();
                    break;
                case InterruptPriority.Hard:
                    //  Debug.LogWarning($"<color=red><b>[AI Interrupt]</b></color> Hard interrupt received on {gameObject.name}. Stopping current action.");
                    StopCurrentAction();
                    break;
                case InterruptPriority.Death:
                    StopCurrentAction();
                    this.enabled = false;
                    break;
            }
        }

        private void HandleInterruptSignal(InterruptPriority priority) => ForceInterrupt(priority);

        private void StopCurrentAction()
        {
            if (this.currentAction == null) return;
            var actionToStop = this.currentAction;

            if (this.executionRoutine != null)
            {
                StopCoroutine(this.executionRoutine);
                this.executionRoutine = null;
            }
            if (this.actionRoutine != null)
            {
                StopCoroutine(this.actionRoutine);
                this.actionRoutine = null;
            }

            actionToStop.EndOrAbort(this.ctx.CurrentMutableEntityContext);
            this.currentAction = null;
            this.currentPlanEnumerator = null;
        }
        protected void RefreshTimerCallback()
        {
            this.refreshTimer.Start();
            this.currentPlanEnumerator = null;
        }
    }
}
