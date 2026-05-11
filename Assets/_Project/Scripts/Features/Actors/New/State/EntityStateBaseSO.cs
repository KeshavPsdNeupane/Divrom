using System;
using Kope.Component;
using Kope.Component.Attack;
using Kope.Component.Movement;
using UnityEngine;

namespace Kope.Actor.New {
	public abstract class EntityStateBaseSO : ScriptableObject {
		[SerializeField, Tooltip("Determines if the entity is receptive to external commands (Player/AI) during this state. \n\n" +
			"True: Move, Idle, Attack.\n" +
			"False: Stunned, Knockback, Cinematic.")]
		private bool _isInputReceptive = false;

		protected AnimationStateProfileData _profileData;
		protected IMovementComponent _movementComponent;
		protected IAttackComponent _attackComponent;
		protected IAnimationComponentNew _animationComponent;
		protected IEntityStateManagement _stateManagement;

		public bool CanStateAcceptExternalCommand => this._isInputReceptive;

		public void Init(IEntityStateManagement stateManagement, AnimationStateProfileData profileData, IMovementComponent movementComponent,
			IAttackComponent attackComponent, IAnimationComponentNew animationComponent) {
			this._stateManagement = stateManagement;
			this._movementComponent = movementComponent;
			this._attackComponent = attackComponent;
			this._animationComponent = animationComponent;
			this._profileData = profileData;
		}

		public abstract StateChangeResult EnterState();

		public virtual void ExitState() {
			this._animationComponent.SetDefaultSpeed();
		}
		public abstract void TickUpdate();
		public virtual void TickFixedUpdate() { }


		protected void RequestTransitionToIdle() => this._stateManagement.TransitionToIdle();
	}
}