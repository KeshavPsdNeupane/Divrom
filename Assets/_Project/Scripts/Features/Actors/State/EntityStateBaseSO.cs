using Kope.Component;
using Kope.Component.Movement;
using UnityEngine;

namespace Kope.Actor.States {

	/// <summary>
	/// The fundamental blueprint for all entity states within the Kope framework. 
	/// Combines inspector-configured data profiles with optional runtime behavioral logic.
	/// </summary>
	/// <remarks>
	/// <para><b>Architecture Pattern:</b> This class acts as a template ScriptableObject. It is duplicated 
	/// per entity instance on initialization, converting static asset blueprints into autonomous, stateful 
	/// logic processors that maintain isolated data tracking.</para>
	/// <para><b>Decoupling Strategy:</b> Communicates entirely through abstracted component interfaces 
	/// (<see cref="IMovementComponent"/>, <see cref="IAnimationComponent"/>) resolved from a central runtime registry.</para>
	/// </remarks>
	public abstract class EntityStateBaseSO : ScriptableObject {

		[SerializeField, Tooltip("Determines if the entity is receptive to external commands (Player/AI) during this state. \n\n" +
			"True: Move, Idle, Attack.\n" +
			"False: Stunned, Knockback, Cinematic.")]
		private bool _isInputReceptive = false;

		/// <summary> Snapshot metadata profiling the underlying animation configuration for this state. </summary>
		protected AnimationStateProfileData _profileData;

		protected IMovementComponent _movementComponent;
		protected IAnimationComponent _animationComponent;
		protected IEntityStateManagement _stateManagement;

		/// <summary> Indicates if higher-level input routing systems can override or interrupt this active state. </summary>
		public bool CanStateAcceptExternalCommand => this._isInputReceptive;

		/// <summary> Exposes the current runtime parameters and configuration bounds of the active state. </summary>
		public AnimationStateProfileData ProfileData => this._profileData;

		/// <summary>
		/// Binds required subsystem dependencies and initializes the tracking payload for the duplicated state instance.
		/// </summary>
		public void Init(IEntityStateManagement stateManagement,
			IMovementComponent movementComponent,
			IAnimationComponent animationComponent,
			AnimationStateProfileData? profileData = default) {
			this._stateManagement = stateManagement;
			this._movementComponent = movementComponent;
			this._animationComponent = animationComponent;
			this._profileData = profileData ?? AnimationStateProfileData.DEFAULT;
		}

		/// <summary>
		/// Safely updates the execution speed modifier of this state at runtime.
		/// </summary>
		/// <remarks>
		/// Because <see cref="AnimationStateProfileData"/> is an immutable struct, this updates the value 
		/// safely using a non-destructive copy-and-mutate structure to protect underlying historical records.
		/// </remarks>
		/// <param name="spd">The targeted playback multiplier. If null, preserves the current speed setting.</param>
		public void ChangeAnimationSpeed(float? spd = null) {
			var pd = this._profileData;
			this._profileData = new(pd.Name, pd.AbsoluteAnimationLength,
				spd ?? pd.AnimationSpeed, pd.IsLooping, pd.NormalizedExitTime);
		}

		/// <summary>
		/// Evaluates whether the state machine can legally transition out of this active state 
		/// into an incoming target state profile.
		/// </summary>
		/// <param name="newState">The structural configuration profile data of the requested successor state.</param>
		/// <returns>A status flag indicating whether the transition was allowed or denied.</returns>
		public StateChangeResult CheckStateChangeFeasibility(AnimationStateProfileData newState) {
			if (!this.CanStateAcceptExternalCommand) return StateChangeResult.Denied_Locked;
			return this._animationComponent.EvaluateTransitionFeasibility(this._profileData).ToStateChangeResult();
		}

		/// <summary>
		/// Triggered immediately on the frame this state becomes the active processor in the state machine hierarchy.
		/// </summary>
		/// <returns>The execution result of the opening frame animation playback attempt.</returns>
		public virtual StateChangeResult EnterState() {
			return this._animationComponent.PlayAnimation(this._profileData, true).ToStateChangeResult();
		}

		/// <summary>
		/// Triggered on the frame this state relinquishes control, serving as a cleanup block to reset global states.
		/// </summary>
		public virtual void ExitState() {
			this._animationComponent.SetDefaultSpeed();
		}

		/// <summary> Frame-rate independent simulation cycle updated directly by the parent entity's Update processor. </summary>
		public abstract void TickUpdate();

		/// <summary> Frame-rate synchronized physics tracking cycle updated directly by the parent entity's FixedUpdate processor. </summary>
		public virtual void TickFixedUpdate() { }

		/// <summary>
		/// Internal routing command telling the central state controller to drop active execution and safely route the entity back to baseline.
		/// </summary>
		protected void RequestTransitionToIdle() => this._stateManagement.TransitionToIdle();
	}


	/// <summary>
	/// Serves as the universal, zero-boilerplate runtime executor for data-driven states.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Architectural Purpose:</b> Prevents null reference overhead by providing a concrete 
	/// <see cref="EntityStateBaseSO"/> instance for inspector definitions that do not require custom, 
	/// script-coded state logic (e.g., simple hit-reacts, basic stuns, or generic attacks).
	/// </para>
	/// <para>
	/// <b>Execution Cycle:</b> This state delegates its lifecycle entirely to visual playback. It acts 
	/// as a simple wrapper that sustains activity for the duration of the targeted clip, relinquishing 
	/// control immediately upon cross-fade completion or clip termination.
	/// </para>
	/// </remarks>
	[CreateAssetMenu(fileName = "DefaultAnimationStateSO", menuName = "Scriptable Objects/Entity States/Default")]
	public class DefaultAnimationStateSO : EntityStateBaseSO {

		/// <summary>
		/// Evaluates playback milestones on the frame update tick to drive automatic state termination.
		/// </summary>
		public override void TickUpdate() {
			// Evaluates the backing animation layer against configured profile markers (e.g., normalized time thresholds)
			if (this._animationComponent.IsAnimationFinished(this._profileData)) {
				// Relinquishes control back to the baseline routing state (Idle) without external intervention
				RequestTransitionToIdle();
			}
		}
	}
}