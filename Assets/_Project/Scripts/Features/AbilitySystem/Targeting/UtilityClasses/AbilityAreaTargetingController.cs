using Kope.Core.Extensions;
using UnityEngine;

public class AbilityAreaTargetingController : MonoBehaviour {
	[SerializeField] private Collider2D _areaCollider2D;
	[SerializeField] private Rigidbody2D _rigidbody2D;

	private float _radius = 5f;

	private void OnValidate() {
		if (this._areaCollider2D == null) {
			Debug.LogWarning($"[AreaTargeting] No Collider2D found on {this.gameObject.name}. " +
			$"Disabling the component.{this.GetFullHierarchyPath()}", this);
			return;
		}
		if (this._rigidbody2D == null) {
			Debug.LogWarning($"[AreaTargeting] No Rigidbody2D found on {this.gameObject.name}." +
			$" Disabling the component.{this.GetFullHierarchyPath()}", this);
			return;
		}
		this._areaCollider2D.isTrigger = true;
		this._rigidbody2D.gravityScale = 0f;
	}

	public void Initialize(float radius) {
		this._radius = radius;

		if (this._areaCollider2D != null) {
			switch (this._areaCollider2D) {
				case CircleCollider2D circle:
					circle.radius = this._radius;
					break;

				case BoxCollider2D box:
					box.size = new Vector2(this._radius * 2f, this._radius * 2f);
					break;

				case CapsuleCollider2D capsule:
					capsule.size = new Vector2(this._radius * 2f, this._radius * 2f);
					break;
			}
		}
		// Optional: keep visual in sync if you still have sprite/mesh
		this.transform.localScale = new Vector3(this._radius * 2f, this._radius * 2f, 1f);
	}

	public void UpdatePosition(Vector3 position, Quaternion rotation) {
		if (this._rigidbody2D != null) {
			this._rigidbody2D.MovePosition(position);
			this._rigidbody2D.MoveRotation(rotation.eulerAngles.z);
		} else {
			this.transform.SetPositionAndRotation(position, rotation);
		}
	}
}