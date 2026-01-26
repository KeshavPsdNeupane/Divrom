using System.Collections;
using System.Collections.Generic;
using Kope.Core.Init;
using Kope.Component.Interfaces;
using UnityEngine;
using Kope.AI.Algorithm;

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
        private AIBrainAlgorithm planner;

        [SerializeField, Tooltip("Components used for context. Only those implementing IInterruptOther will be subscribed for interrupts.")]
        private List<InitializableBase> components;

        [SerializeField, Tooltip("The entity context provider supplying the current state of the entity.")]
        private EntityStateController entityStateController;


        private EntityContext ctx;
        private BaseActionSO currentAction;
        private Coroutine actionRoutine;
        private IEnumerator<BaseActionSO> currentPlanEnumerator;

        private readonly List<IInterruptOther> interrupters = new();

        public override void Init()
        {
            if (this.IsInitialized) return;

            // Initialize context
            this.ctx = new EntityContext(
                  entityStateController.StateMachine,
                  entityStateController.EntityStates
              );

            // Register all components in context
            foreach (var comp in components)
            {
                // adding component to context based on its actual type 
                // it wont allow duplicate types,only one component per type
                // since a entity will only have one health component for example
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
            if (!this.IsInitialized || this.planner == null) return;

            // 1. Logic Guard: If we are busy, don't rethink unless the plan is empty
            if (this.currentAction != null && !this.currentAction.IsCompleted)
                return;

            // I dont think we need to have Update Context function
            // since all the components in the context are references
            // so any mutation to their state is reflected in the context already
            // with out any extra update call, so commenting this out for now

            // 2. Planning Phase
            if (this.currentPlanEnumerator == null)
                FetchNewPlan();

            // 3. Execution Phase
            FollowCurrentPlan();
        }


        protected virtual void FetchNewPlan()
        {
            var plan = this.planner.GetDecisionPlan(this.ctx);
            if (plan == null) return;

            this.currentPlanEnumerator = plan.GetEnumerator();
            this.currentAction = null;
        }

        protected virtual void FollowCurrentPlan()
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
        {
            ForceInterrupt(priority);
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



    }
}