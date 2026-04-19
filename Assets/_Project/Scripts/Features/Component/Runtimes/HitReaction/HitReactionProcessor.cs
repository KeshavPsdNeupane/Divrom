using Kope.Component.HitBox.Interface;
using Kope.Component.Movement;
using Kope.Core.EntityComponentRegistry;
using Kope.Core.Init;
using UnityEngine;

namespace Kope.Component.HitReaction {

	/// <summary>
	/// This component is responsible for processing hit reactions such as stuns and knockbacks on an entity.
	/// It listens for hits on the attached HurtBox and applies stun and knockback effects accordingly.
	/// If a entity his this component, then that entity is expected fight back to player, so be careful where you put this.<br/>
	/// <b> Important: </b><br/>
	/// - This component assumes that the entity has a HurtBoxComponent for it to function properly. <br/>
	/// - The main reason of seperation of this component from the HurtBoxComponent is to allow for more flexible and modular hit reaction processing logic, as well as to keep the HurtBoxComponent focused solely on detecting hits and related mechanics. <br/>
	/// - and not all entity needs below whole complex hit reaction processing, for example,
	///  a destructible environment might just need to get destroyed on a single hit, we can just create 1HitEntityComponent.
	/// 	which will handle that event rather than this bloat of component. <br/>
	/// </summary>
	public class HitReactionProcessor : InitializableBase {
		[SerializeField] private EntityComponentsRegistry ecr;

		private IHitBoxComponent hurtBox;
		private IStunnable stunnable;
		private IKnockbackable knockbackable;

		protected override bool OnInit() {
			if (this.ecr == null) {
				Debug.LogError($"HitReactionProcessor on {gameObject.name} has no ECR assigned.");
				return false;
			}

			if (!this.ecr.ComponentRegistry.TryGetMutatableComponent(out this.hurtBox)) {
				Debug.LogError($"HitReactionProcessor on {gameObject.name} failed to find HurtBox.");
				return false;
			}

			this.ecr.ComponentRegistry.TryGetMutatableComponent(out this.stunnable, false);
			this.ecr.ComponentRegistry.TryGetMutatableComponent(out this.knockbackable, false);

			return true;
		}

		private void OnEnable() {
			if (this.hurtBox == null) return;
			this.hurtBox.OnHitStunnable += HandleHurtBoxStun;
			this.hurtBox.OnHitKnockable += HandleHurtBoxKnockback;
		}

		private void OnDisable() {
			if (this.hurtBox != null) {
				this.hurtBox.OnHitStunnable -= HandleHurtBoxStun;
				this.hurtBox.OnHitKnockable -= HandleHurtBoxKnockback;
			}
		}

		private void HandleHurtBoxStun(StunnableHitInfo hitInfo) {
			if (!this.IsInitialized || this.stunnable == null || hitInfo.StunEffects == null) return;

			for (int i = 0; i < hitInfo.StunEffects.Count; i++) {
				var effect = hitInfo.StunEffects[i]?.Create(hitInfo.EffectContext);
				effect?.Apply(this.stunnable);
			}
		}

		private void HandleHurtBoxKnockback(KnockableHitInfo hitInfo) {
			if (!this.IsInitialized || this.knockbackable == null || hitInfo.KnockEffects == null) return;

			for (int i = 0; i < hitInfo.KnockEffects.Count; i++) {
				var effect = hitInfo.KnockEffects[i]?.Create(hitInfo.EffectContext);
				effect?.Apply(this.knockbackable);
			}
		}
	}
}