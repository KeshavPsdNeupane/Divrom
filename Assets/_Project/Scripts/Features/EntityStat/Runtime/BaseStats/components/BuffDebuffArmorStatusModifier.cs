using UnityEngine;
using ThirdParty;
namespace Kope.Character.Stats {
	[System.Serializable]
	public class BuffDebuffArmorStatusModifier {
		[HideInInspector] public bool canRemove = false;
		[System.NonSerialized] public CountdownTimer durationCountDownTimer;

		[SerializeField] private AbstractBaseModifier baseEffect;

		public bool IsDebuff => this.baseEffect.IsDebuff;
		public string EffectName => this.baseEffect.effectName;
		public string Source => this.baseEffect.source;
		public float ModifierAmount => this.baseEffect.ModifierAmount;
		public bool IsPercentage => this.baseEffect.isPercentage;
		public bool IsDebuffFromArmor => this.baseEffect.isDebuffFromArmor;
		public bool IsDebuffFromEnemy => this.baseEffect.isDebuffFromEnemy;
		public int DebuffPriority => this.baseEffect.debuffPriority;
		public bool IsPermanentBuff => this.baseEffect.IsPermanentEffect;



		public BuffDebuffArmorStatusModifier(AbstractBaseModifier effect) {
			this.baseEffect = effect;
		}

		public void InitializeTimer() {
			if (this.baseEffect == null) return;

			if (this.durationCountDownTimer == null)
				this.durationCountDownTimer = new CountdownTimer(baseEffect.totalDuration);
			else
				this.durationCountDownTimer.Reset(baseEffect.totalDuration);
		}

		public void StartTimer() {
			this.durationCountDownTimer?.Start();
		}

	}
}