using Kope.Core;
using Kope.Component.Movement;
using UnityEngine;
public class FOVGizmo : MonoBehaviour {
	[SerializeField] private EntitySensor sensor;
	[SerializeField] Color outerLineColor = Color.yellow;
	[SerializeField] Color innerLineColor = Color.red;
	[SerializeField] private bool visualizeInnerDetectionRadius = true;
	[SerializeField] private Color innerDetectionRadiusColor = Color.gray;
	[SerializeField] MovementComponentBase mover;

	void OnValidate() {
		if (this.sensor == null) {
			Debug.LogWarning($"[FOVGizmo] EntitySensor reference is not assigned on {gameObject.name}. " +
			"Please assign it in the inspector to visualize the FOV correctly.", this.gameObject);
			return;
		}
		if (this.mover == null) {
			Debug.LogWarning($"[FOVGizmo] MovementComponentBase reference is not assigned on {gameObject.name}. " +
			"Please assign it in the inspector to visualize the FOV correctly.");
			return;
		}

	}

	private void OnDrawGizmosSelected() {
		if (this.mover == null || this.sensor == null) return;

		var fovData = this.sensor.FieldOfViewData;
		float halfAngle = fovData.FieldOfViewAngle * 0.5f;
		float range = fovData.ViewDistance;

		Vector3 lookDir;
		try {
			lookDir = mover.GetLookingAtDirection();

		} catch {
			lookDir = new Vector3(0, -1, 0);
		}
		Vector3 origin = transform.position;

		bool is2D = mover.Dimension == AxisMode.TwoD;
		Vector3 axis = is2D ? Vector3.forward : Vector3.up;

		Vector3 leftDir = Quaternion.AngleAxis(-halfAngle, axis) * lookDir;
		Vector3 rightDir = Quaternion.AngleAxis(halfAngle, axis) * lookDir;
		Gizmos.color = outerLineColor;
		Gizmos.DrawRay(origin, leftDir * range);
		Gizmos.DrawRay(origin, rightDir * range);
		Gizmos.color = innerLineColor;
		Gizmos.DrawRay(origin, lookDir * range);
		if (this.visualizeInnerDetectionRadius) {
			Gizmos.color = this.innerDetectionRadiusColor;
			Gizmos.DrawWireSphere(origin, fovData.InnerDetectionRadius);
		}
	}
}