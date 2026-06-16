using Kope.Component;
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
		protected IAnimationComponentNew _animationComponent;
		protected IEntityStateManagement _stateManagement;

		public bool CanStateAcceptExternalCommand => this._isInputReceptive;
		public AnimationStateProfileData ProfileData => this._profileData;
		public void Init(IEntityStateManagement stateManagement,
		IMovementComponent movementComponent, IAnimationComponentNew animationComponent,
		 AnimationStateProfileData? profileData = default) {
			this._stateManagement = stateManagement;
			this._movementComponent = movementComponent;
			this._animationComponent = animationComponent;
			this._profileData = profileData ?? AnimationStateProfileData.DEFAULT;
		}

		public void ChangeAnimationSpeed(float? spd = null) {
			var pd = this._profileData;
			this._profileData = new(pd.Name, pd.AbsoluteAnimationLength,
			spd ?? pd.AnimationSpeed, pd.IsLooping, pd.NormalizedExitTime);
		}

		public StateChangeResult CheckStateChangeFeasibility(AnimationStateProfileData newState) {
			if (!this.CanStateAcceptExternalCommand) return StateChangeResult.Denied_Locked;
			return this._animationComponent.EvaluateTransitionFeasibility(this._profileData).ToStateChangeResult();
		}


		public virtual StateChangeResult EnterState() {
			return this._animationComponent.PlayAnimation(this._profileData, true).ToStateChangeResult();
		}


		public virtual void ExitState() {
			this._animationComponent.SetDefaultSpeed();
		}
		public abstract void TickUpdate();
		public virtual void TickFixedUpdate() { }


		protected void RequestTransitionToIdle() => this._stateManagement.TransitionToIdle();
	}
}