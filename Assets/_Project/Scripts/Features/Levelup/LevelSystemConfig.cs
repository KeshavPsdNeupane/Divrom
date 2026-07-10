using UnityEngine;


namespace Kope.ExperienceSystem.Config {

	[CreateAssetMenu(fileName = "LevelSystemConfig", menuName = "Configs/Level System")]
	public class LevelSystemConfig : ScriptableObject {
		[SerializeField] private int maxLevel = 100;
		[SerializeField] private float baseExp = 100;

		[Header("Dynamic Exponent settings")]
		[Tooltip("The power curve multiplier at low levels (e.g., Level 2). Keeps early levels fast.")]
		[SerializeField] private float startPower = 1.5f;

		[Tooltip("The power curve multiplier at max level. Creates a heavy endgame grind.")]
		[SerializeField] private float endPower = 2.4f;

		private float[] _expRequiredForLevel;
		public float[] ExpRequiredForLevel => _expRequiredForLevel;

		private void OnEnable() {
			CreateExpRequiredForLevel();
		}

		private void OnValidate() {
			if (this.maxLevel <= 1) this.maxLevel = 2;
			if (this.baseExp <= 0) this.baseExp = 1;
			if (this.startPower <= 0.5f) this.startPower = 0.5f;
			if (this.endPower < this.startPower) this.endPower = this.startPower;

			CreateExpRequiredForLevel();
		}

		private void CreateExpRequiredForLevel() {
			this._expRequiredForLevel = new float[this.maxLevel];
			this._expRequiredForLevel[0] = 0; // Level 1 is free

			float cumulativeExp = 0;

			for (int i = 1; i < this.maxLevel; i++) {
				float progress = (float)i / (this.maxLevel - 1);
				float currentPower = Mathf.Lerp(this.startPower, this.endPower, progress);
				float exp = this.baseExp * Mathf.Pow(i, currentPower) + this.baseExp;

				cumulativeExp += exp;
				this._expRequiredForLevel[i] = cumulativeExp;
			}
		}
		public float GetCumulativeXpForLevel(int level) {
			if (level < 1) return 0;
			if (level > this.maxLevel) return this._expRequiredForLevel[this.maxLevel - 1];
			return this._expRequiredForLevel[level - 1];
		}



		public int GetLevelFromCumulativeXp(float currentXp) {
			if (currentXp <= 0) return 1;
			if (currentXp >= this._expRequiredForLevel[this.maxLevel - 1]) return this.maxLevel;

			int low = 0;
			int high = this.maxLevel - 1;
			int highestValidIndex = 0;

			while (low <= high) {
				int mid = low + (high - low) / 2;
				if (currentXp >= this._expRequiredForLevel[mid]) {
					highestValidIndex = mid;
					low = mid + 1;
				} else {
					high = mid - 1;
				}
			}
			return highestValidIndex + 1;
		}
	}
}