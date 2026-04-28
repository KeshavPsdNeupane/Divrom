using Kope.Core.Extensions;
using UnityEngine;

public class AbilityAreaTargetingController : MonoBehaviour {
	/*
		Why is this class "passive" compared to the Projectile Controller?

		1. Lifecycle Dependency: Unlike projectiles, an Area Preview's life is tied 1:1 
		   to the active Targeting Strategy. It has no reason to exist once the player 
		   stops aiming. Therefore, it does not need internal 'Release' logic; the 
		   Strategy handles its cleanup as part of the FinishTheStrategy routine.

		2. Behavior Responsibility: This class acts as a 'Data Sink'—it simply receives 
		   position and radius data from the Strategy and applies it to its local 
		   components (Colliders, Sprites, VFX). By keeping it "dumb," we avoid 
		   duplicate logic between the targeting math (Strategy) and the visual 
		   representation (this Controller).

		3. Performance: Since the Strategy already possesses an active 'Update' loop 
		   to track the mouse, letting the Strategy tick the Preview avoids adding 
		   hundreds of independent 'Update' calls to the Unity engine if multiple 
		   previews were somehow active.
	*/
	[SerializeField] private Collider2D _areaCollider2D;
	[SerializeField] private Rigidbody2D _rigidbody2D;
	[SerializeField] private SpriteRenderer _areaSpriteRenderer;
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
		if (this._areaSpriteRenderer == null) {
			Debug.LogWarning($"[AreaTargeting] No SpriteRenderer found on {this.gameObject.name}." +
			$" Disabling the component.{this.GetFullHierarchyPath()}", this);
			return;
		}
		this._areaCollider2D.isTrigger = true;
		this._rigidbody2D.gravityScale = 0f;
	}

	public void Initialize(Vector3 position, float radius, Color? areaColor = null) {
		this._radius = radius;
		this.transform.position = position;
		if (areaColor.HasValue) {
			this._areaSpriteRenderer.color = areaColor.Value;
		}


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