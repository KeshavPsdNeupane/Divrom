using System.Collections.Generic;
using Kope.Core.Init;
using Kope.Component.Interfaces;
using UnityEngine;
using ThirdParty;
using System;
using Kope.Core.EntityComponentSystem;

namespace Kope.AI
{

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
        private IEnumerator<BaseActionSO> currentPlanEnumerator;
        private CountdownTimer refreshTimer;
        private readonly List<IInterruptOther> interrupters = new();
        #endregion

        public override void OnInit()
        {
            base.OnInit();

            if (this.ecs == null || this.planner == null)
            {
                Debug.LogError($"AIBrain Error: Missing ECS or Planner on {gameObject.name}" +
                 GetParentGameObjectStackTraceMessage());
                return;
            }

            if (!this.ecs.ComponentRegistry.TryGetComponent(out this.entityStateController))
            {
                Debug.LogError($"AIBrain Error: EntityStateController not found on {gameObject.name}" +
                 GetParentGameObjectStackTraceMessage());
                return;
            }

            this.ctx = new Context(this.ecs.ComponentRegistry);

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

        protected override void OnUpdate()
        {
            base.OnUpdate();

            if (!IsBrainValid()) return;

            UpdateInternalTimers();

            if (HandleStateMachine()) return;

            HandleActionCompletion();

            if (this.currentAction == null)
            {
                TryAdvancePlan();
            }
            TickCurrentAction();
        }

        protected override void OnFixedUpdate()
        {
            base.OnFixedUpdate();
            if (!IsBrainValid()) return;
            if (!this.entityStateController.CanStateMachineAcceptCommand) return;

            TickCurrentActionPhysic();

        }

        #region Update Logic Chunks
        protected virtual bool IsBrainValid() => this.planner != null && this.ecs != null;

        protected virtual void UpdateInternalTimers()
        {
            refreshTimer?.Tick(Time.deltaTime);
        }

        /// <summary>
        /// Returns true if the entity is physically unable to execute AI commands.
        /// </summary>
        protected virtual bool HandleStateMachine()
        {
            if (!this.entityStateController.CanStateMachineAcceptCommand)
            {
                if (this.currentAction != null) StopCurrentAction();
                return true;
            }
            return false;
        }

        protected virtual void HandleActionCompletion()
        {
            if (this.currentAction != null && this.currentAction.IsCompleted)
            {
                this.currentAction.EndOrAbort(this.ctx.CurrentMutableEntityContext);
                this.currentAction = null;
            }
        }

        protected virtual void TryAdvancePlan()
        {
            if (this.currentPlanEnumerator == null) FetchNewPlan();

            if (this.currentPlanEnumerator != null && this.currentAction == null)
            {
                ExecuteNextActionInPlan();
            }
        }

        protected virtual void ExecuteNextActionInPlan()
        {
            if (this.currentPlanEnumerator.MoveNext())
            {
                var nextAction = this.currentPlanEnumerator.Current;
                if (nextAction != null)
                {
                    this.currentAction = nextAction;
                    this.currentAction.Initialize(this.ctx.CurrentMutableEntityContext);
                }
            }
            else
            {
                this.currentPlanEnumerator = null;
                this.currentAction = null; // Ensure we are clean if MoveNext is false
            }
        }

        protected virtual void TickCurrentAction()
        {
            if (this.currentAction != null && !this.currentAction.IsCompleted)
            {
                this.currentAction.TickUpdate(this.ctx);
            }
        }

        protected virtual void TickCurrentActionPhysic()
        {
            if (this.currentAction != null && !this.currentAction.IsCompleted)
            {
                this.currentAction.TickFixedUpdate(this.ctx);
            }
        }
        #endregion

        #region Helpers & Callbacks
        protected virtual void FetchNewPlan()
        {
            var plan = this.planner.GetDecisionPlan(this.ctx);
            if (plan != null)
            {
                this.currentPlanEnumerator = plan.GetEnumerator();
            }
        }

        protected virtual void StopCurrentAction()
        {
            if (this.currentAction != null)
            {
                this.currentAction.EndOrAbort(this.ctx.CurrentMutableEntityContext);
            }
            this.currentAction = null;
            this.currentPlanEnumerator = null;
        }

        private void HandleInterruptSignal(InterruptPriority priority) => ForceInterrupt(priority);

        public virtual void ForceInterrupt(InterruptPriority priority = InterruptPriority.Soft)
        {
            switch (priority)
            {
                case InterruptPriority.Soft:
                    //Debug.Log($"[AIBrain] Soft Interrupt received on {gameObject.name}. Will attempt to stop current action if it is interruptible." + GetParentGameObjectStackTraceMessage());
                    this.currentPlanEnumerator = null;
                    if (this.currentAction != null && this.currentAction.IsInterruptible)
                        StopCurrentAction();
                    break;
                case InterruptPriority.Hard:
                    // Debug.Log($"[AIBrain] Hard Interrupt received on {gameObject.name}. Forcing stop of current action." + GetParentGameObjectStackTraceMessage());
                    StopCurrentAction();
                    break;
                case InterruptPriority.Death:
                    // Debug.Log($"[AIBrain] Death Interrupt received on {gameObject.name}. Forcing stop of current action and disabling AI." + GetParentGameObjectStackTraceMessage());
                    StopCurrentAction();
                    this.enabled = false;
                    break;
            }
        }

        protected virtual void RefreshTimerCallback()
        {
            this.refreshTimer.Start();
            this.currentPlanEnumerator = null;
        }
        #endregion
    }

}
