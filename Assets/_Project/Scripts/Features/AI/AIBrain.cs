using System.Collections.Generic;
using Kope.Core.LifeTimeManagement;
using Kope.Component.Interfaces;
using UnityEngine;
using ThirdParty;
using System;
using Kope.Core.EntityComponentRegistry;
using Kope.Actor.New;
using Kope.EntityComponentSystem;

namespace Kope.AI {

	public class AIBrain : ComponentBase, IUpdatable, IFixedUpdatable {
		#region Inspector Fields
		[SerializeField] private EntityComponentsRegistry ecr;
		[SerializeField, Tooltip("The AI brain algorithm that defines the decision-making logic.")]
		private AIBrainAlgorithm planner;
		[SerializeField, Tooltip("Components used for context. Only those implementing IInterruptOther will be subscribed.")]
		private List<ComponentBase> components;

		[SerializeField, Range(0f, 20f), Tooltip("Interval to force the brain to refresh its plan periodically. Set to 0 to disable.")]
		private float refreshInterval = 1.0f;

		[SerializeField] private EntitySensor sensor;

		[Header("Debug Utilities")]
		[SerializeField, Tooltip("If checked, the planner will be initialized when the brain is initialized.")]
		private bool initPlannerOnBrainInit = true;
		#endregion

		#region Private Fields
		private Context _ctx;

		private EntityStateManagement _entityStateManagement;
		private BaseActionSO _currentAction;
		private IEnumerator<BaseActionSO> _currentPlanEnumerator;
		private CountdownTimer _refreshTimer;
		private readonly List<IInterruptOther> _interrupters = new();
		#endregion

		protected override bool OnInit() {
			if (this.ecr == null || this.planner == null) {
				Debug.LogError($"AIBrain Error: Missing ECS or Planner on {gameObject.name}" +
				 this.HieararchyPath);
				return false;
			}

			if (!this.ecr.TryFetchMutable(this, this.HieararchyPath, out this._entityStateManagement)) {
				return false; // error is being logged inside TryFetchMutable
			}
			this._ctx = new Context(this.ecr.ComponentRegistry);

			foreach (var comp in components) {
				this._ctx.CurrentMutableEntityContext.Register(comp);
				if (comp is IInterruptOther interrupter) {
					interrupter.OnInterruptRequested -= HandleInterruptSignal;
					interrupter.OnInterruptRequested += HandleInterruptSignal;
					this._interrupters.Add(interrupter);
				}
			}

			if (this.initPlannerOnBrainInit) {
				this.planner.Init();
			}

			if (this.refreshInterval > 0f) {
				this._refreshTimer = new CountdownTimer(this.refreshInterval);
				this._refreshTimer.OnTimerStop += RefreshTimerCallback;
				this._refreshTimer.Start();
			}
			this.sensor.InitContext(this._ctx);
			return true;
		}

		protected override void OnDestroy() {
			foreach (var interrupter in this._interrupters)
				interrupter.OnInterruptRequested -= HandleInterruptSignal;
		}

		public void OnUpdate() {
			if (!IsBrainValid()) return;

			UpdateInternalTimers();

			if (HandleStateMachine()) return;

			HandleActionCompletion();

			if (this._currentAction == null) {
				TryAdvancePlan();
			}
			TickCurrentAction();

		}

		public void OnFixedUpdate() {
			if (!IsBrainValid()) return;
			if (!this._entityStateManagement.CanStateAcceptExternalCommand) return;

			TickCurrentActionPhysic();

		}

		#region Update Logic Chunks
		protected virtual bool IsBrainValid()
		=> this.planner != null && this.ecr != null && this.planner.IsInitialized;

		protected virtual void UpdateInternalTimers() {
			this._refreshTimer?.Tick(Time.deltaTime);
		}

		/// <summary>
		/// Returns true if the entity is physically unable to execute AI commands.
		/// </summary>
		protected virtual bool HandleStateMachine() {
			if (!this._entityStateManagement.CanStateAcceptExternalCommand) {
				if (this._currentAction != null) StopCurrentAction();
				return true;
			}
			return false;
		}

		protected virtual void HandleActionCompletion() {
			if (this._currentAction != null && this._currentAction.IsCompleted) {
				this._currentAction.EndOrAbort();
				this._currentAction = null;
			}
		}

		protected virtual void TryAdvancePlan() {
			if (this._currentPlanEnumerator == null) FetchNewPlan();

			if (this._currentPlanEnumerator != null && this._currentAction == null) {
				ExecuteNextActionInPlan();
			}
		}

		protected virtual void ExecuteNextActionInPlan() {
			if (this._currentPlanEnumerator.MoveNext()) {
				var nextAction = this._currentPlanEnumerator.Current;
				if (nextAction != null) {
					this._currentAction = nextAction;
					this._currentAction.Initialize(this._ctx);
				}
			} else {
				this._currentPlanEnumerator = null;
				this._currentAction = null; // Ensure we are clean if MoveNext is false
			}
		}

		protected virtual void TickCurrentAction() {
			if (this._currentAction != null && !this._currentAction.IsCompleted) {
				this._currentAction.TickUpdate();
			}
		}

		protected virtual void TickCurrentActionPhysic() {
			if (this._currentAction != null && !this._currentAction.IsCompleted) {
				this._currentAction.TickFixedUpdate();
			}
		}
		#endregion

		#region Helpers & Callbacks
		protected virtual void FetchNewPlan() {
			var plan = this.planner.GetDecisionPlan(this._ctx);
			if (plan != null) {
				this._currentPlanEnumerator = plan.GetEnumerator();
			}
		}

		protected virtual void StopCurrentAction() {
			if (this._currentAction != null) {
				this._currentAction.EndOrAbort();
			}
			this._currentAction = null;
			this._currentPlanEnumerator = null;
		}

		private void HandleInterruptSignal(InterruptPriority priority) => ForceInterrupt(priority);

		public virtual void ForceInterrupt(InterruptPriority priority = InterruptPriority.Soft) {
			switch (priority) {
				case InterruptPriority.Soft:
					//Debug.Log($"[AIBrain] Soft Interrupt received on {gameObject.name}. Will attempt to stop current action if it is interruptible." + GetParentGameObjectStackTraceMessage());
					this._currentPlanEnumerator = null;
					if (this._currentAction != null && this._currentAction.IsInterruptible)
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
			this._refreshTimer.Start();
			this._currentPlanEnumerator = null;
		}
		#endregion
	}

}
