using Kope.Component.Animation;
using Kope.Component.Movement;
using Kope.Core.EntityComponentRegistry;
using Kope.Core.Init;
using UnityEngine;

namespace Kope.Actor {
	public class AnimationStateController : InitializableBase {
		[SerializeField] private EntityComponentsRegistry ecr;
		private ILastDirectionProvider _lastDirectionProvider;
		private IAnimationComponent _animationComponent;

		protected override bool OnInit() {
			if (ecr == null) {
				Debug.LogError("AnimationStateController requires an EntityComponentsRegistry reference." +
				$"{GetParentGameObjectHeirarchyMessage()}", this);
				return false;
			}
			if (!ecr.ComponentRegistry.TryGetMutatableComponent(out _animationComponent)) {
				Debug.LogError("EntityComponentsRegistry does not contain a component that implements IAnimationComponent." +
				$"{GetParentGameObjectHeirarchyMessage()}", this);
				return false;
			}
			if (!ecr.ComponentRegistry.TryGetMutatableComponent(out _lastDirectionProvider)) {
				Debug.LogError("EntityComponentsRegistry does not contain a component that implements ILastDirectionProvider." +
				$"{GetParentGameObjectHeirarchyMessage()}", this);
				return false;
			}
			return true;
		}

		Vector3 _tempLastDir;
		protected override void OnUpdate() {
			base.OnUpdate();
			// it wont spam the log.
			if (this._tempLastDir != _lastDirectionProvider.LastDirection) {
				this._tempLastDir = _lastDirectionProvider.LastDirection;
				Debug.Log($"Last direction changed to: {this._tempLastDir}");
			}
		}
	}
}
