using System;
using Kope.Actor.New;
using Kope.Component;
using Kope.Component.Attack;
using Kope.Component.Movement;
using UnityEngine;
namespace Kope.Feature.AnimationState {
	public abstract class EntityStateBaseSO : ScriptableObject {
		[SerializeField, Tooltip("Determines if the entity is receptive to external commands (Player/AI) during this state. \n\n" +
			"True: Move, Idle, Attack.\n" +
			"False: Stunned, Knockback, Cinematic.")]
		private bool _isInputReceptive = false;
		private AnimationStateProfileData _profileData;
		private IMovementComponent _movementComponent;
		private IAttackComponent _attackComponent;
		private IAnimationComponentNew _animationComponent;


		/// <summary>
		/// THis event is triggered when the state determines that a transition to the Idle state should occur.
		/// like after attacking state/animation is finished, or after a knockback/stun duration ends. The 
		/// EntityStateManagement listens to this event to handle the actual transition logic.
		/// </summary>
		public event Action TransitionToIdleTrigger;

		public bool CanStateAcceptExternalCommand => this._isInputReceptive;
		/// <summary>
		/// Initializes the EntityState with required components. This method should be called before using 
		/// the state to ensure all dependencies are properly set.
		/// </summary>
		/// <param name="movementComponent"></param>
		/// <param name="attackComponent"></param>
		/// <param name="animationComponent"></param>

		public void Init(AnimationStateProfileData profileData, IMovementComponent movementComponent, IAttackComponent attackComponent,
		IAnimationComponentNew animationComponent) {
			this._movementComponent = movementComponent;
			this._attackComponent = attackComponent;
			this._animationComponent = animationComponent;
			this._profileData = profileData;
		}

		public abstract void EnterState();
		public abstract void ExitState();
		public abstract void UpdateState();
		public virtual void FixedUpdateState() { }



		public void SubscribeToStateTrigger(Action callback) {
			this.TransitionToIdleTrigger += callback;
		}
		public void UnsubscribeFromStateTrigger(Action callback) {
			this.TransitionToIdleTrigger -= callback;
		}
	}
}