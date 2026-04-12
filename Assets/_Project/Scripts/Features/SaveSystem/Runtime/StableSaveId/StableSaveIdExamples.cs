using Kope.Component.Health;
using Kope.Component.Movement;
using Kope.SaveSystem;

namespace Kope.SaveSystem.Examples {
	[SaveId("player_movement")]
	public class PlayerMovementComponentExample : PlayerMovementComponent {
	}

	[SaveId("health")]
	public class HealthComponentExample : HealthComponentBase {
	}
}
