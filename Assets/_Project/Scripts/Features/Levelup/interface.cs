
namespace Kope.ExperienceSystem.Interface {
	public interface IExperienceSystem {
		float CurrentExp { get; }
		int CurrentLevel { get; }
		event System.Action<int> OnLevelChanged;
		void AddExperience(float amount);
		void LevelChangeEvent(System.Action<int> callback, bool isSubscribe);
		void SimulateLevelUp(); // will be removed in the future, only for testing purposes
	}

}