using Kope.Core.LifeTimeManagement;
using UnityEngine;

public class CharacterLevel : InitializableBase {

	[SerializeField] private int currentLevel = 1;
	public int CurrentLevel => this.currentLevel;

}
