using System;
using UnityEngine;
using Kope.Core.EntityComponentRegistry;
using Kope.AI.AIBlackBoard;
using System.Runtime.CompilerServices;


namespace Kope.AI.Interface {
	public interface IGizmoDrawable {
		void DrawGizmos();
	}
}
namespace Kope.AI {
	public enum ExecutionActionStatus : short {
		NotInitialized = 0,
		Running = 10,
		Success = 20,
		Failure = 99
	}


	/// <summary>
	/// Base action type for any AI system.
	/// Defines the minimal interface for execution and completion.
	/// </summary>
	public abstract class BaseActionSO : ScriptableObject {
		[SerializeField] protected string actionName = "Base Action";
		[SerializeField] protected bool isInterruptible = true;
		protected ExecutionActionStatus actionStatus = ExecutionActionStatus.NotInitialized;

		public string ActionName => actionName;
		public bool IsInterruptible => isInterruptible;
		public bool IsCompleted => actionStatus != ExecutionActionStatus.Running && actionStatus != ExecutionActionStatus.NotInitialized;

		public event Action OnActionCompleted;

		#region Unity Callbacks
#if UNITY_EDITOR
		protected void OnValidate() => ResetState();
#endif
		void OnEnable() => ResetState();

		protected void ResetState() {
			actionStatus = ExecutionActionStatus.NotInitialized;
			OnActionCompleted = null;
		}
		#endregion

		/// <summary>
		/// Initializes the action with the given context.
		/// Cache everything you need for the action in this method,
		/// so you can use it in the tick updates without worrying about performance issues.
		/// Always call this method to Properly initialize the action before ticking it. 
		/// Otherwise, it may lead to unintended consequences since the action is not properly initialized.
		/// </summary>
		/// <param name="ctx"></param>
		public void Initialize(Context ctx) {
			this.actionStatus = ExecutionActionStatus.Running;
			OnInitialize(ctx);
		}


		/// <summary>
		/// A Template Method hook for action initialization within the provided <see cref="Context"/>.
		/// </summary>
		/// <remarks>
		/// <para><b>Architectural Contract:</b> The <see cref="Context"/> provides access to target entities via 
		/// <see cref="IReadOnlyComponentRegistry"/>. While these interfaces are read-only, the underlying 
		/// implementation is a reference type. <b>Do not mutate target data through these references.</b></para>
		/// 
		/// <para>Modifying external entity state during initialization violates the ECS decoupling 
		/// principle and introduces non-deterministic side-effects. Mutation is strictly permitted 
		/// only for the 'Self' entity context to facilitate internal state setup.</para>
		/// 
		/// <para><b>Temporal Safety:</b> Since <c>Update</c> and <c>FixedUpdate</c> do not receive a context 
		/// parameter, all required data must be cached during this call. Do not persist a reference to 
		/// the context beyond this method's scope to avoid unintended stale-data access. And clean all
		/// the references on function <c>OnEndOrAbort</c>.</para>
		/// </remarks>
		/// <param name="ctx">The evaluation context containing the self-entity and environmental targets.</param>
		protected abstract void OnInitialize(Context ctx);

		/// <summary>
		/// End or abort the action.
		/// Always override this method when needed.
		/// First clean up any state related to the action,
		/// do validations, then call base.EndOrAbort(ctx) to reset status.
		/// </summary>
		public void EndOrAbort() {
			OnEndOrAbort();
			actionStatus = ExecutionActionStatus.NotInitialized;
			OnActionCompleted = null;
		}


		protected abstract void OnEndOrAbort();
		public abstract void TickUpdate();
		public abstract void TickFixedUpdate();


		/// <summary>
		/// Mark the action as completed.
		/// </summary>
		public void MarkCompleted() {
			actionStatus = ExecutionActionStatus.Success;
			OnActionCompleted?.Invoke();
		}
	}
}
