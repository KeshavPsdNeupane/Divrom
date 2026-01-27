using System.Collections;
using System.Collections.Generic;
using Kope.Core.Init;
using Kope.Component.Interfaces;
using UnityEngine;
using ThirdParty;


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
        // This is a mono behaviour so we can attach it to game objects directly
        // and have it work as a component in the entity system
        [SerializeField, Tooltip("Mandatory: The transform of the entity this brain controls.")]
        private Transform entityTransform;

        [SerializeField, Tooltip("The AI brain algorithm that defines the decision-making logic.")]
        private AIBrainAlgorithm planner;

        [SerializeField, Tooltip("Components used for context. Only those implementing IInterruptOther will be subscribed for interrupts.")]
        private List<InitializableBase> components;

        [SerializeField, Tooltip("The entity context provider supplying the current state of the entity.")]
        private EntityStateController entityStateController;

        [SerializeField, Range(0f, 20f), Tooltip("In Second, This " +
        "This interval Forces the brain to refresh its plan periodically.If the current plan is really long lived " +
        "this will help the AI to rethink its plan more often." +
        "Set to 0 to disable automatic refreshing.")]
        private float refreshInterval = 1.0f;
        #endregion


        #region Private Fields
        private EntityContext ctx;
        private BaseActionSO currentAction;
        private Coroutine actionRoutine;
        private IEnumerator<BaseActionSO> currentPlanEnumerator;
        private CountdownTimer refreshTimer;
        private readonly List<IInterruptOther> interrupters = new();
        #endregion


        public override void Init()
        {
            if (this.IsInitialized) return;
            base.Init();
            if (this.entityTransform == null)
            {
                Debug.LogError("AIBrain Initialization Failed: Entity Transform is not assigned.");
                return;
            }
            if (this.entityStateController == null)
            {
                Debug.LogError("AIBrain Initialization Failed: Entity State Controller is not assigned.");
                return;
            }
            if (this.planner == null)
            {
                Debug.LogError("AIBrain Initialization Failed: AI Brain Algorithm (planner) is not assigned.");
                return;
            }
            // Initialize context
            this.ctx = new EntityContext(
                this.entityTransform,
                this.entityStateController.StateMachine,
                this.entityStateController.EntityStates
            );

            // Register all components in context
            foreach (var comp in components)
            {
                this.ctx.AddComponent(comp);
                // Only subscribe if component implements IInterruptOther
                if (comp is IInterruptOther interrupter)
                {
                    // preemtive unsubscribe to avoid multiple subscriptions
                    interrupter.OnInterruptRequested -= HandleInterruptSignal;
                    interrupter.OnInterruptRequested += HandleInterruptSignal;
                    this.interrupters.Add(interrupter);
                }
            }

            // Initialize planner, No need to put on InitLifecycleManager since
            //  since brain Init manages it directly
            this.planner.Init();

            // SO the we dont init cooldown timer when refresh interval is 0
            // since that means no auto refresh
            if (this.refreshInterval > 0f)
            {
                this.refreshTimer = new CountdownTimer(this.refreshInterval);
                this.refreshTimer.OnTimerStop += RefreshTimerCallback;
                this.refreshTimer.Start();
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe only from components that implement IInterruptOther
            foreach (var interrupter in this.interrupters)
                interrupter.OnInterruptRequested -= HandleInterruptSignal;
        }

        protected override void Update()
        {
            base.Update();
            if (!this.IsInitialized) return;

            this.refreshTimer?.Tick(Time.deltaTime);

            if (!this.IsInitialized || this.planner == null) return;
            // 1. Logic Guard: If we are busy, don't rethink unless the plan is empty
            if (this.currentAction != null && !this.currentAction.IsCompleted)
                return;
            if (this.currentPlanEnumerator == null)
                FetchNewPlan();
            // 3. Execution Phase
            ExecuteThePlan();
        }


        protected virtual void FetchNewPlan()
        {
            var plan = this.planner.GetDecisionPlan(this.ctx);
            if (plan == null) return;

            this.currentPlanEnumerator = plan.GetEnumerator();
            this.currentAction = null;
        }

        protected virtual void ExecuteThePlan()
        {
            if (this.currentAction != null && !this.currentAction.IsCompleted) return;

            if (this.currentPlanEnumerator != null && this.currentPlanEnumerator.MoveNext())
                ExecuteNewAction(this.currentPlanEnumerator.Current);
            else
            {
                this.currentPlanEnumerator = null;
                this.currentAction = null;
            }
        }

        protected virtual void ExecuteNewAction(BaseActionSO action)
        {
            if (action == null)
            {
                Debug.LogError("ExecuteNewAction called with null ActionSO!");
                return;
            }
            this.currentAction = action;
            this.actionRoutine = StartCoroutine(RunActionSequence(action));
        }

        protected virtual IEnumerator RunActionSequence(BaseActionSO action)
        {
            action.Initialize(this.ctx);
            bool actionFinished = false;

            void completionHandler() => actionFinished = true;
            action.OnActionCompleted += completionHandler;

            // Execute action coroutine
            yield return StartCoroutine(action.Execute(this.ctx));
            // wait until action signals completion, either via event or IsCompleted flag
            // just in case action.Execute doesn't yield until completion or as a safeguard
            yield return new WaitUntil(() => actionFinished || action.IsCompleted);

            action.OnActionCompleted -= completionHandler;
            action.EndOrAbort(this.ctx);
        }

        /// <summary>
        /// Forces an interrupt on the current action and plan based on the specified priority.<br/>
        /// Soft: Interrupts only if the current action is interruptible.
        /// force brain to replan the path after completing current uninterruptable action<br/>
        /// Hard: Interrupts regardless of interruptibility.<br/>
        /// Death: Interrupts and disables the brain entirely.<br/>
        /// </summary>
        /// <param name="priority"></param>
        public virtual void ForceInterrupt(InterruptPriority priority = InterruptPriority.Hard)
        {
            switch (priority)
            {
                // marking the current plan as null to force replanning after current action
                // if the current action is interruptible we stop it immediately
                case InterruptPriority.Soft:
                    if (this.currentAction != null && this.currentAction.IsInterruptible)
                        StopCurrentAction();
                    else
                        this.currentPlanEnumerator = null;
                    break;
                case InterruptPriority.Hard:
                case InterruptPriority.Death:
                    StopCurrentAction();
                    if (priority == InterruptPriority.Death)
                        this.enabled = false;
                    break;
            }
            Debug.Log($"AI Brain on {this.gameObject.name} interrupted with priority: {priority}");
        }

        private void HandleInterruptSignal(InterruptPriority priority)
        => ForceInterrupt(priority);

        private void StopCurrentAction()
        {
            if (this.actionRoutine != null) { StopCoroutine(this.actionRoutine); }
            if (this.currentAction != null) { this.currentAction.EndOrAbort(this.ctx); }
            this.currentAction = null;
            this.currentPlanEnumerator = null;
        }


        protected void RefreshTimerCallback()
        {
            this.currentPlanEnumerator = null;
            this.refreshTimer.Reset();
        }


    }
}