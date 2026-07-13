using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
using Kope.Core.LifeTimeManagement;
using Kope.Character.Stats;
using Kope.Core.EntityComponentRegistry;
using Kope.Component.Health.Interface;


public class StatDescription : InitializableBase {
	[SerializeField] private GameObject statDescriptionUIPanel;
	[SerializeField] private EntityComponentsRegistry ecr;

	[SerializeField] private TMP_FontAsset fontAsset;

	private CharacterStatsSystemBase _characterStats;
	private IHealthComponent _healthComponent;


	private readonly Dictionary<CharacterStatType, UnityAction<float>> _statsCallbacksDict = new();
	private readonly Dictionary<CharacterStatType, TextMeshProUGUI> _statTextObjects = new();
	private RectTransform _panelRect; // cache panelRect to avoid fetching multiple times

	protected override bool OnInit() {
		return Validate();
	}
	private void ResolveHpDisplay(float currentHp, float maxHp) {
		if (this._statTextObjects.TryGetValue(CharacterStatType.HP, out var tmp)) {
			tmp.text = $"{CharacterStatType.HP}: {currentHp:0}/{maxHp:0}";
		}
	}
	private void SetCurrentHp(HealthChangeInfo healthChangeInfo) {
		ResolveHpDisplay(healthChangeInfo.CurrentHealth, healthChangeInfo.MaxHealth);
	}

	void OnEnable() {
		if (this.IsInitialized) {
			// with Init , no need to Validate again here since OnEnable will only be 
			// called after successful Init, but we do need to check for nulls to avoid errors 
			// in case of unexpected issues.
			CreateTMPObjects();
			SubscribeToStats();
		}
	}

	void OnDisable() {
		if (!this.IsInitialized) return;
		this._healthComponent.OnHealthChange(SetCurrentHp, false);

		foreach (var kvp in this._statsCallbacksDict)
			this._characterStats.StatsUnsubscribe(kvp.Key, kvp.Value);

		this._statsCallbacksDict.Clear();
	}

	private bool Validate() {
		if (ecr == null) {
			Debug.LogError("EntityComponentStore reference was missing," +
		  " unable to retrieve CharacterStatsSystem." + this.HieararchyPath);
			return false;
		}
		// using tryGet since this only shows the stats on UI and does not modify the stats,
		//  so we don't need mutatable access here. so TryGetComponent is sufficient for semantic clarity.
		if (!ecr.ComponentRegistry.TryGetReadOnly<CharacterStatsSystemBase>(out var statsSystem)) {
			Debug.LogError("No CharacterStatsSystem found in EntityComponentStore for StatDescription" + this.HieararchyPath);
			return false;

		}
		this._characterStats = statsSystem;

		if (!ecr.ComponentRegistry.TryGetReadOnly<IHealthComponent>(out var healthComponent)) {
			Debug.LogError("No IHealthComponent found in EntityComponentStore for StatDescription" + this.HieararchyPath);
			return false;

		}
		this._healthComponent = healthComponent;

		if (this.statDescriptionUIPanel == null) {
			Debug.LogError("StatDescriptionUIPanel is not assigned." + this.HieararchyPath);
			return false;
		}
		if (this._panelRect == null && this.statDescriptionUIPanel != null) {
			this._panelRect = this.statDescriptionUIPanel.GetComponent<RectTransform>();
			if (this._panelRect == null)
				Debug.LogError("Stat Panel requires RectTransform." + this.HieararchyPath);
		}
		return true;
	}

	private void CreateTMPObjects() {
		if (this._panelRect == null) return;

		int numberOfStats = Enum.GetValues(typeof(CharacterStatType)).Length;
		float panelHeight = this._panelRect.rect.height;
		float lineHeight = panelHeight / (numberOfStats + 1);

		int index = 0;
		foreach (CharacterStatType type in Enum.GetValues(typeof(CharacterStatType))) {
			if (_statTextObjects.ContainsKey(type)) continue;

			GameObject textGO = new(type.ToString());
			textGO.transform.SetParent(statDescriptionUIPanel.transform, false);

			TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
			tmp.font = fontAsset;
			tmp.alignment = TextAlignmentOptions.Left;
			tmp.color = Color.black;
			tmp.text = $"{type}: 0";

			RectTransform rt = tmp.GetComponent<RectTransform>();
			rt.anchorMin = new Vector2(0, 1);
			rt.anchorMax = new Vector2(0, 1);
			rt.pivot = new Vector2(0, 1);
			rt.anchoredPosition = new Vector2(10f, -index * lineHeight);
			rt.sizeDelta = new Vector2(_panelRect.rect.width - 20f, lineHeight);

			tmp.enableAutoSizing = true;
			tmp.fontSizeMin = 4;
			tmp.fontSizeMax = 200;

			_statTextObjects[type] = tmp;
			index++;
		}
	}

	private void SubscribeToStats() {
		if (this._characterStats == null || this._characterStats.CurrentStats == null
		|| this._healthComponent == null) return;

		// initial setup and fetching current/max health to display on UI, and subscribe to 
		// changes to update the display when health changes.
		var currentHealth = this._healthComponent.CurrentHealth;
		var maxHealth = this._healthComponent.MaxHealth;
		this._healthComponent.OnHealthChange(SetCurrentHp, true);
		ResolveHpDisplay(currentHealth, maxHealth);



		// subscribe to all stat changes to update the UI when any stat changes. 
		// we use a dictionary to keep track of the callbacks for each stat so
		// we can unsubscribe them later.
		foreach (CharacterStatType type in Enum.GetValues(typeof(CharacterStatType))) {
			// check if the stat exists to avoid errors
			if (type == CharacterStatType.HP || !this._characterStats.CurrentStats.ContainsKey(type)) continue;

			float initialValue = this._characterStats.CurrentStats[type].GetValue();

			void callback(float val) {
				if (this._statTextObjects.TryGetValue(type, out var tmp))
					tmp.text = $"{type}: {val:0}";
			}

			// Unsubscribe previous callback if exists
			if (this._statsCallbacksDict.TryGetValue(type, out var oldCallback))
				this._characterStats.StatsUnsubscribe(type, oldCallback);

			this._statsCallbacksDict[type] = callback;
			this._characterStats.StatsSubscribe(type, callback);

			// Immediately update TMP text
			if (this._statTextObjects.TryGetValue(type, out var text)) {
				text.text = $"{type}: {initialValue:0}";
			}
		}

	}
}

