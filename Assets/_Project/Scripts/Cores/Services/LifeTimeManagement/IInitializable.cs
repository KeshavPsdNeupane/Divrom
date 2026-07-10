
namespace Kope.Core.LifeTimeManagement {
	public interface IInitializable {

		bool IsInitialized { get; }
		void Init();
		void CheckInit();

	}

	public interface IUpdatable {
		void OnUpdate();
	}
	public interface IFixedUpdatable {
		void OnFixedUpdate();
	}
}