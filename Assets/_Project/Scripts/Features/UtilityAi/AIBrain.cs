using System;
using System.Collections;
using System.Collections.Generic;
using Kope.Core.Init;
using Kope.Component.Interfaces;
using UnityEngine;
using UnityEditor.Toolbars;

namespace Kope.AI.Brain
{
    /// <summary>
    /// The AI brain component responsible for decision-making.
    /// It utilizes a specified AI brain algorithm to generate action plans
    /// based on the entity's current context.
    /// </summary>
    public class AIBrain : InitializableBase
    {
        // the AI brain algorithm ScriptableObject used only for Initialization for the instance
        // the actual brain instance is created during Init()
        [SerializeField, Tooltip("The AI brain algorithm that defines the decision-making logic.")]
        private AIBrainAlgorithmSO brainAlgorithmSO;

        [SerializeField, Tooltip("Components used for context. Only those implementing IInterruptOther will be subscribed for interrupts.")]
        private List<InitializableBase> components;

        [SerializeField, Tooltip("The entity context provider supplying the current state of the entity.")]
        private EntityStateController entityStateController;

        private AIBrainAlgorithmSO brain;
        private EntityContext ctx;
        private ActionSO currentAction;
        private Coroutine actionRoutine;
        private IEnumerator<ActionSO> currentPlanEnumerator;

        private readonly List<IInterruptOther> interrupters = new();

        public override void Init()
        {
            this.brain = Instantiate(this.brainAlgorithmSO);
            // Initialize context
            this.ctx = new EntityContext(
                  entityStateController.StateMachine,
                  entityStateController.EntityStates
              );

            // Register all components in context
            foreach (var comp in components)
            {
                this.ctx.AddComponent(comp.GetType().Name, comp);

                // Only subscribe if component implements IInterruptOther
                if (comp is IInterruptOther interrupter)
                {
                    // preemtive unsubscribe to avoid multiple subscriptions
                    interrupter.OnInterruptRequested -= HandleInterruptSignal;
                    interrupter.OnInterruptRequested += HandleInterruptSignal;
                    this.interrupters.Add(interrupter);
                }
            }

            base.Init();
        }

        private void OnDestroy()
        {
            // Unsubscribe only from components that implement IInterruptOther
            foreach (var interrupter in this.interrupters)
                interrupter.OnInterruptRequested -= HandleInterruptSignal;
        }


        private void Update()
        {
            if (!this.IsInitialized || this.brain == null) return;

            // 1. Logic Guard: If we are busy, don't rethink unless the plan is empty
            if (this.currentAction != null && !this.currentAction.IsCompleted)
                return;

            UpdateContext();

            // 2. Planning Phase
            if (this.currentPlanEnumerator == null)
                FetchNewPlan();

            // 3. Execution Phase
            FollowCurrentPlan();
        }
        private void UpdateContext()
        {
            // Update dynamic runtime data here (health, positions, cooldowns)
        }

        private void FetchNewPlan()
        {
            var plan = this.brain.GetDecisionPlan(this.ctx);
            if (plan == null) return;

            this.currentPlanEnumerator = plan.GetEnumerator();
            this.currentAction = null;
        }

        private void FollowCurrentPlan()
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

        private void ExecuteNewAction(ActionSO action)
        {
            this.currentAction = action;
            this.actionRoutine = StartCoroutine(RunActionSequence(action));
        }

        private IEnumerator RunActionSequence(ActionSO action)
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
        public void ForceInterrupt(InterruptPriority priority = InterruptPriority.Hard)
        {
            switch (priority)
            {
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

            Debug.Log($"<color=yellow>{gameObject.name} Brain Force Interrupted! Priority: {priority}</color>");
        }

        private void StopCurrentAction()
        {
            if (this.actionRoutine != null)
                StopCoroutine(this.actionRoutine);

            if (this.currentAction != null)
                this.currentAction.EndOrAbort(this.ctx);
            this.currentAction = null;
            this.currentPlanEnumerator = null;
        }

        private void HandleInterruptSignal(InterruptPriority priority)
        {
            ForceInterrupt(priority);
        }

    }
}