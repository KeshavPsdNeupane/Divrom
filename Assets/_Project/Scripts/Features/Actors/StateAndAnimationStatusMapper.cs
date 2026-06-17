using Kope.Actor.New;
namespace Kope.Actor {
	public static class AnimationStatusExtensions {
		public static StateChangeResult ToStateChangeResult(this AnimationStatus status) {
			return status switch {
				AnimationStatus.Success => StateChangeResult.Success,
				AnimationStatus.NotFound => StateChangeResult.Error_NotFound,
				AnimationStatus.InTransition => StateChangeResult.Denied_Locked,
				AnimationStatus.Busy => StateChangeResult.Denied_Busy,
				_ => StateChangeResult.Failed
			};
		}
	}
}