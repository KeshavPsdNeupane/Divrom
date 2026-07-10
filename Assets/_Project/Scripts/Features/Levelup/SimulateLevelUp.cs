using Kope.ExperienceSystem;
using UnityEngine;
using UnityEngine.UI;

public class SimulateLevelUp : MonoBehaviour {
	[SerializeField] private ExperienceSystem levelSystem;
	[SerializeField] private Button button;
	[ContextMenu("Simulate Level Up")]

	private void Start() {
		if (this.button != null) {
			this.button.onClick.AddListener(SimulateLevelUpMethod);
		}
	}
	public void SimulateLevelUpMethod() {
		if (this.levelSystem != null) {
			this.levelSystem.SimulateLevelUp();
		} else {
			Debug.LogWarning("LevelSystem reference is not assigned in SimulateLevelUp.");
		}
	}
}
