

namespace Kope.Component.Movement {
	public interface IStunnable {
		public void Stun(float duration);
		public void SuperStun(float duration);
		public void ForceCancellStun();
	}
}
