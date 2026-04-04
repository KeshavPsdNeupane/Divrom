using Kope.Component.Movement;
using UnityEngine;

public enum Dimension { TwoD, ThreeD }
public class LookingDirection : MonoBehaviour {
	[SerializeField] private MovementComponentBase movementComponent;
	[SerializeField] private float gizmoLineLength = 5f;
	[SerializeField] private Color gizmoColor = Color.red;

	private void OnDrawGizmos() {
		if (this.movementComponent == null) return;
		Vector3 dir = this.movementComponent.GetLookingAtDirection().normalized;
		Gizmos.color = gizmoColor;
		Gizmos.DrawLine(transform.position, transform.position + dir * gizmoLineLength);
	}
}