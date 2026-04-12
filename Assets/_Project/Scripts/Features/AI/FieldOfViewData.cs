// # Credit/Author: Kope(#me) :D 



using UnityEngine;
/// <summary>
/// A struct that holds data related to an entity's field of view, including the angle,
/// view distance, and inner detection radius. It also precomputes values for efficient calculations 
/// during runtime, such as the cosine of the angle threshold and the squared distances 
/// for view and inner detection radius.
/// This struct is designed to be immutable and can be used to easily manage 
/// and access field of view data for AI entities in a game.
/// </summary>
namespace Kope.Component {
	public readonly struct FieldOfViewData {
		private readonly float fieldOfViewAngle;
		private readonly float viewDistance;
		private readonly float innerDetectionRadius;
		public readonly float FieldOfViewAngle => this.fieldOfViewAngle;
		public readonly float ViewDistance => this.viewDistance;
		public readonly float InnerDetectionRadius => this.innerDetectionRadius;
		public readonly float CosineOfAngleThreshold;
		public readonly float SquareCosineOfAngleThreshold;
		public readonly float SquareViewDistance;
		public readonly float SquareInnerDetectionRadius;

		public FieldOfViewData(float fieldOfViewAngle = 90f, float viewDistance = 10f, float innerDetectionRadius = 1f) {
			this.fieldOfViewAngle = fieldOfViewAngle;
			this.viewDistance = viewDistance;
			this.innerDetectionRadius = innerDetectionRadius;

			this.SquareViewDistance = this.viewDistance * this.viewDistance;
			this.SquareInnerDetectionRadius = this.innerDetectionRadius * this.innerDetectionRadius;
			this.CosineOfAngleThreshold = Mathf.Cos(fieldOfViewAngle * 0.5f * Mathf.Deg2Rad);
			this.SquareCosineOfAngleThreshold = CosineOfAngleThreshold * CosineOfAngleThreshold;
		}
	}
}
