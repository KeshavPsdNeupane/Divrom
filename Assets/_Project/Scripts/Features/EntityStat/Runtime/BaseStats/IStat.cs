using UnityEngine.Events;

namespace Kope.Character.Stats {
	public interface IStat {
		public event UnityAction<float> OnStatsModified;

		public void Update();
		public void OnEnable();
		public void OnDisable();
		public float GetValue();
	}

}