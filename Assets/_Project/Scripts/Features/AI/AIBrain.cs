using System.Collections.Generic;
using Kope.Core.Init;
using Kope.Component.Interfaces;
using UnityEngine;
using ThirdParty;
using System;
using Kope.Core.EntityComponentSystem;

namespace Kope.AI {

	public class AIBrain : InitializableBase {
		#region Inspector Fields
		[SerializeField] private EntityComponentsRegistry ecr;
		[SerializeField, Tooltip("The AI brain algorithm that defines the decision-making logic.")]
		private AIBrainAlgorithm planner;
		[SerializeField, Tooltip("Components used for context. Only those implementing IInterruptOther will be subscribed.")]
		private List<InitializableBase> components;
		[SerializeField, Range(0f, 20f), Tooltip("Interval to force the brain to refresh its plan periodically. Set to 0 to disable.")]
		private float refreshInterval = 1.0f;

		[SerializeField] private EntitySensor sensor;

		[Header("Debug Utilities")]
		[SerializeField, Tooltip("If checked, the planner will be initialized when the brain is initialized.")]
		private bool initPlannerOnBrainInit = true;
		#endregion

		#region Private Fields
		private Context ctx;
		private EntityStateController entityStateController;
		private BaseActionSO currentAction;
		private IEnumerator<BaseActionSO> currentPlanEnumerator;
		private CountdownTimer refreshTimer;
		private readonly List<IInterruptOther> interrupters = new();
		#endregion

		public override bool OnInit() {
			if (this.ecr == null || this.planner == null) {
				Debug.LogError($"AIBrain Error: Missing ECS or Planner on {gameObject.name}" +
				 GetParentGameObjectHeirarchyMessage());
				return false;
			}

			if (!this.ecr.ComponentRegistry.TryGetMutatableComponent(out this.entityStateController)) {
				Debug.LogError($"AIBrain Error: EntityStateController not found on {gameObject.name}" +
				 GetParentGameObjectHeirarchyMessage());
				return false;
			}

			this.ctx = new Context(this.ecr.ComponentRegistry);

			foreach (var comp in components) {
				this.ctx.CurrentMutableEntityContext.Register(comp);
				if (comp is IInterruptOther interrupter) {
					interrupter.OnInterruptRequested -= HandleInterruptSignal;
					interrupter.OnInterruptRequested += HandleInterruptSignal;
					this.interrupters.Add(interrupter);
				}
			}

			if (this.initPlannerOnBrainInit) {
				this.planner.OnInit();
			}

			if (this.refreshInterval > 0f) {
				this.refreshTimer = new CountdownTimer(this.refreshInterval);
				this.refreshTimer.OnTimerStop += RefreshTimerCallback;
				this.refreshTimer.Start();
			}
			Debug.Log("Sensor Init Context called from AIBrain OnInit");
			this.sensor.InitContext(this.ctx);
			Debug.Log($"[AIBrain] Initialization complete for {gameObject.name}. Context and Planner are set up." + GetParentGameObjectHeirarchyMessage());
			return true;
		}

		private void OnDestroy() {
			foreach (var interrupter in this.interrupters)
				interrupter.OnInterruptRequested -= HandleInterruptSignal;
		}

		protected override void OnUpdate() {
			base.OnUpdate();

			if (!IsBrainValid()) return;

			UpdateInternalTimers();

			if (HandleStateMachine()) return;

			HandleActionCompletion();

			if (this.currentAction == null) {
				TryAdvancePlan();
			}
			TickCurrentAction();
		}

		protected override void OnFixedUpdate() {
			base.OnFixedUpdate();
			if (!IsBrainValid()) return;
			if (!this.entityStateController.CanStateMachineAcceptCommand) return;

			TickCurrentActionPhysic();

		}

		#region Update Logic Chunks
		protected virtual bool IsBrainValid()
		=> this.planner != null && this.ecr != null && !this.planner.IsInitialized;
		// need to check if the planner is initialized, because the brain might run its update before the 
		// planner's OnInit is called,
		//  and we don't want it to try to fetch a plan before the planner is ready.

		protected virtual void UpdateInternalTimers() {
			refreshTimer?.Tick(Time.deltaTime);
		}

		/// <summary>
		/// Returns true if the entity is physically unable to execute AI commands.
		/// </summary>
		protected virtual bool HandleStateMachine() {
			if (!this.entityStateController.CanStateMachineAcceptCommand) {
				if (this.currentAction != null) StopCurrentAction();
				return true;
			}
			return false;
		}

		protected virtual void HandleActionCompletion() {
			if (this.currentAction != null && this.currentAction.IsCompleted) {
				this.currentAction.EndOrAbort(this.ctx.CurrentMutableEntityContext);
				this.currentAction = null;
			}
		}

		protected virtual void TryAdvancePlan() {
			if (this.currentPlanEnumerator == null) FetchNewPlan();

			if (this.currentPlanEnumerator != null && this.currentAction == null) {
				ExecuteNextActionInPlan();
			}
		}

		protected virtual void ExecuteNextActionInPlan() {
			if (this.currentPlanEnumerator.MoveNext()) {
				var nextAction = this.currentPlanEnumerator.Current;
				if (nextAction != null) {
					this.currentAction = nextAction;
					this.currentAction.Initialize(this.ctx.CurrentMutableEntityContext);
				}
			} else {
				this.currentPlanEnumerator = null;
				this.currentAction = null; // Ensure we are clean if MoveNext is false
			}
		}

		protected virtual void TickCurrentAction() {
			if (this.currentAction != null && !this.currentAction.IsCompleted) {
				this.currentAction.TickUpdate(this.ctx);
			}
		}

		protected virtual void TickCurrentActionPhysic() {
			if (this.currentAction != null && !this.currentAction.IsCompleted) {
				this.currentAction.TickFixedUpdate(this.ctx);
			}
		}
		#endregion

		#region Helpers & Callbacks
		protected virtual void FetchNewPlan() {
			var plan = this.planner.GetDecisionPlan(this.ctx);
			if (plan != null) {
				this.currentPlanEnumerator = plan.GetEnumerator();
			}
		}

		protected virtual void StopCurrentAction() {
			if (this.currentAction != null) {
				this.currentAction.EndOrAbort(this.ctx.CurrentMutableEntityContext);
			}
			this.currentAction = null;
			this.currentPlanEnumerator = null;
		}

		private void HandleInterruptSignal(InterruptPriority priority) => ForceInterrupt(priority);

		public virtual void ForceInterrupt(InterruptPriority priority = InterruptPriority.Soft) {
			switch (priority) {
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

		protected virtual void RefreshTimerCallback() {
			this.refreshTimer.Start();
			this.currentPlanEnumerator = null;
		}
		#endregion
	}

}
