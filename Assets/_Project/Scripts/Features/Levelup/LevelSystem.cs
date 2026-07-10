using Kope.Core.LifeTimeManagement;
using UnityEngine;

public class LevelSystem : InitializableBase {
	[Header("Level System Config")]
	[SerializeField] private LevelSystemConfig levelSystemConfig;

	[Header("Batching Settings")]
	[SerializeField, Tooltip("How often to check for level-ups (in seconds)")]
	private float batchCheckInterval = 0.2f;
	[SerializeField, Tooltip("Delay before the first level-up check (in seconds)")]
	private float batchInitialCheckDelay = 2f;



	private int _currentExp = 0;
	private int _currentLevel = 1;
	private int _lastCheckedExp = 0;
	public event System.Action<int> OnLevelChanged;
	public int CurrentExp => this._currentExp;
	public int CurrentLevel => this._currentLevel;
	protected override bool OnInit() {
		this._currentExp = 0;
		if (this.levelSystemConfig == null) {
			Debug.LogError($"LevelSystemConfig is not assigned in LevelSystem.+{GetParentGameObjectHeirarchyMessage()}");
			return false;
		}
		InvokeRepeating(nameof(HandleLevelUpBatching), this.batchInitialCheckDelay, this.batchCheckInterval);
		return true;
	}
	/// <summary>
	/// Adds experience points to the current experience total. If the amount is less 
	/// than or equal to zero, the method will exit without making any changes.
	/// It waits for the next batch check to determine if a level-up has occurred, 
	/// rather than checking immediately.
	/// </summary>
	/// <param name="amount"></param>
	public void AddExperience(int amount) {
		if (amount <= 0) return;
		this._currentExp += amount;
	}


	private void HandleLevelUpBatching() {
		if (this._currentExp == this._lastCheckedExp) return;

		this._lastCheckedExp = this._currentExp;

		int newLevel = this.levelSystemConfig.GetLevelFromCumulativeXp(this._currentExp);
		if (newLevel != this._currentLevel) {
			this._currentLevel = newLevel;
			this.OnLevelChanged?.Invoke(this._currentLevel);
		}
	}

	/// <summary>
	/// Subscribes or unsubscribes a callback to the level change event.
	/// </summary>
	/// <remarks>
	/// Reuses a single method with a boolean toggle to simplify event lifecycle management. 
	/// To ensure safe event handling and prevent duplicate registrations, it automatically 
	/// unsubscribes the callback before performing a new subscription.
	/// </remarks>
	/// <param name="callback">The method to invoke with the new level value when a level change occurs.</param>
	/// <param name="isSubscribe">True to register the callback; False to remove it.</param>
	public void LevelChangeEvent(System.Action<int> callback, bool isSubscribe) {
		if (isSubscribe) {
			this.OnLevelChanged -= callback;
			this.OnLevelChanged += callback;
		} else {
			this.OnLevelChanged -= callback;
		}
	}


	protected override void OnDestroy() {
		// first child then parent.
		CancelInvoke(nameof(HandleLevelUpBatching));
		base.OnDestroy();
	}
}
