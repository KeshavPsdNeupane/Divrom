using System.Collections;
using System.Collections.Generic;
using Kope.Core.Init;
using Kope.Component.Interfaces;
using UnityEngine;
using ThirdParty;
using System;
using Kope.Core.EntityComponentSystem;

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

        [SerializeField, Tooltip("Components used for context. Only those implementing IInterruptOther will be subscribed for interrupts.")]
        private List<InitializableBase> components;



        [SerializeField, Range(0f, 20f), Tooltip("In Second, This " +
        "This interval Forces the brain to refresh its plan periodically.If the current plan is really long lived " +
        "this will help the AI to rethink its plan more often." +
        "Set to 0 to disable automatic refreshing.")]
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
            if (this.ecs == null)
            {
                Debug.LogError("AIBrain Initialization Failed: Entity Component Store is not assigned." + GetParentGameObjectStackTraceMessage());
                return;
            }
            if (this.planner == null)
            {
                Debug.LogError("AIBrain Initialization Failed: AI Brain Algorithm (planner) is not assigned." + GetParentGameObjectStackTraceMessage());
                return;
            }
            if (!this.ecs.ComponentRegistry.TryGetComponent(out this.entityStateController))
            {
                Debug.LogError("AIBrain Initialization Failed: EntityStateController component not found in EntityComponentStore." + GetParentGameObjectStackTraceMessage());
                return;
            }

            this.ctx = new Context(this.ecs.ComponentRegistry);

            // Register all components in context
            foreach (var comp in components)
            {
                this.ctx.CurrentMutableEntityContext.AddComponent(comp);
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
            this.planner.OnInit();

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




        protected override void OnUpdate()
        {
            base.OnUpdate();
            // bail out if these are null, no need to check the EntityStateController since
            // ecs probably wont be null if we have entityStateController
            if (this.planner == null || this.ecs == null) return;

            // if state machine cannot accept commands, bail out
            if (!this.entityStateController.CanStateMachineAcceptCommand)
            {
                if (currentAction != null) StopCurrentAction();
                return;
            }

            // always tick the refresh timer if enabled
            refreshTimer?.Tick(Time.deltaTime);

            // 1. If current action is still running, do nothing
            if (currentAction != null && !currentAction.IsCompleted) return;
            // 2. If no current plan, fetch a new one
            if (currentPlanEnumerator == null) FetchNewPlan();
            // 3. Execute the current plan
            ExecuteThePlan();
        }



        protected virtual void FetchNewPlan()
        {
            var newPlan = this.planner.GetDecisionPlan(this.ctx);
            if (newPlan == null) return;


            var planEnumerator = newPlan.GetEnumerator();

            if (planEnumerator.MoveNext())
            {
                BaseActionSO topAction = planEnumerator.Current;

                // bail out if the top action is the same as the current action
                if (currentAction != null &&
                currentAction.GetType() == topAction.GetType()) return;

                // need to call the newPlan.GetEnumerator() again since
                // we already moved planEnumerator once to check the top action
                // and we want to start from the beginning again.
                // that way we dont miss any actions in the plan.
                // even though this feels a bit wasteful, plans are usually short lived
                // and this is simpler than trying to reset the enumerator.
                this.currentPlanEnumerator = newPlan.GetEnumerator();
                this.currentAction = null;
            }
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
            // null check, bail out if null
            // since we can probably get null actions in the plan,
            // we just skip them or treat them as no-ops
            if (action == null) return;
            currentAction = action;
            this.actionRoutine = StartCoroutine(RunActionSequence(action));
        }

        protected virtual IEnumerator RunActionSequence(BaseActionSO action)
        {
            action.Initialize(this.ctx.CurrentMutableEntityContext);
            bool actionFinished = false;

            // We define this as a variable so we can safely remove it later
            void handler()
            {
                actionFinished = true;
            }

            action.OnActionCompleted += handler;

            try
            {
                // needs full context for execution since 
                this.executionRoutine = StartCoroutine(action.Execute(this.ctx));
                yield return this.executionRoutine;
                yield return new WaitUntil(() => actionFinished || action.IsCompleted);
            }
            finally
            {
                // Unsubscribe using the variable reference
                action.OnActionCompleted -= handler;

                // Only call EndOrAbort if this hasn't been interrupted/nulled yet
                if (this.currentAction == action)
                {
                    action.EndOrAbort(this.ctx.CurrentMutableEntityContext);
                    this.actionRoutine = null;
                    this.executionRoutine = null;
                    this.currentAction = null;
                }
            }
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
            //  Debug.Log($"AIBrain ({gameObject.name}): ForceInterrupt called with priority {priority}.");
            switch (priority)
            {
                case InterruptPriority.Soft:
                    this.currentPlanEnumerator = null; // Force replan after current action
                    if (this.currentAction != null && this.currentAction.IsInterruptible)
                        StopCurrentAction();
                    break;
                case InterruptPriority.Hard:
                case InterruptPriority.Death:
                    StopCurrentAction();
                    if (priority == InterruptPriority.Death)
                        this.enabled = false;
                    break;
            }
        }

        private void HandleInterruptSignal(InterruptPriority priority)
        => ForceInterrupt(priority);

        private void StopCurrentAction()
        {
            if (this.currentAction == null) return;

            var actionToCleanup = this.currentAction; // Cache it

            // Stop routines
            if (this.executionRoutine != null) StopCoroutine(this.executionRoutine);
            if (this.actionRoutine != null) StopCoroutine(this.actionRoutine);

            this.currentAction = null;
            this.currentPlanEnumerator = null;
            this.executionRoutine = null;
            this.actionRoutine = null;

            // Cleanup
            actionToCleanup.EndOrAbort(this.ctx.CurrentMutableEntityContext);
        }

        protected void RefreshTimerCallback()
        {

            this.refreshTimer.Start(); // restart timer
            this.currentPlanEnumerator = null; // Trigger plan refresh
        }
    }
}