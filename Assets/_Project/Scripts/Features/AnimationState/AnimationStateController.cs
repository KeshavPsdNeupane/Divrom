using System.Collections.Generic;
using Kope.Component.Animation;
using Kope.Component.Attack;
using Kope.Component.Movement;
using Kope.Core.EntityComponentRegistry;
using Kope.Core.Init;
using UnityEngine;

namespace Kope.Actor.New {
	public class EntityStateManagement : InitializableBase {
		[SerializeField] private EntityComponentsRegistry ecr;
		[SerializeField] private AnimationData animationData;
		[SerializeField] private bool loadOnStart = true;
		private IDirectionProvider _lastDirectionProvider;
		private IAnimationComponent _animationComponent;
		private IAttackComponent _attackComponent;
		private Dictionary<int, AnimationStateProfile> _animationStateLookup = new();

		private void Awake() {
			if (this.loadOnStart) {
				Init();
			}
		}

		protected override bool OnInit() {
			if (this.ecr == null) {
				Debug.LogError("AnimationStateController requires an EntityComponentsRegistry reference." +
				$"{GetParentGameObjectHeirarchyMessage()}", this);
				return false;
			}
			if (!this.ecr.ComponentRegistry.TryGetMutatableComponent(out this._animationComponent)) {
				Debug.LogError("EntityComponentsRegistry does not contain a component that implements IAnimationComponent." +
				$"{GetParentGameObjectHeirarchyMessage()}", this);
				return false;
			}
			if (!this.ecr.ComponentRegistry.TryGetMutatableComponent(out this._attackComponent)) {
				Debug.LogError("EntityComponentsRegistry does not contain a component that implements IAttackComponent." +
				$"{GetParentGameObjectHeirarchyMessage()}", this);
				return false;
			}
			if (!this.ecr.ComponentRegistry.TryGetMutatableComponent(out this._lastDirectionProvider)) {
				Debug.LogError("EntityComponentsRegistry does not contain a component that implements ILastDirectionProvider." +
				$"{GetParentGameObjectHeirarchyMessage()}", this);
				return false;
			}
			if (this.animationData.Value == null || this.animationData.Value.Length == 0) {
				Debug.LogError("AnimationData does not contain any AnimationStateProfiles." +
				$"{GetParentGameObjectHeirarchyMessage()}", this);
				return false;
			}
			foreach (var profile in this.animationData.Value) {
				if (!this._animationStateLookup.ContainsKey(profile.Hash)) {
					this._animationStateLookup.Add(profile.Hash, profile);
				} else {
					Debug.LogWarning($"Duplicate AnimationStateHash detected in AnimationData: {profile.Name}. " +
					"Only the first occurrence will be used.", this);
				}
			}


			return true;
		}

		Vector3 _tempLastDir;
		protected override void OnUpdate() {
			base.OnUpdate();
			// it wont spam the log.
			if (this._tempLastDir != this._lastDirectionProvider.Direction) {
				this._tempLastDir = this._lastDirectionProvider.Direction;
			}
		}

	}
}