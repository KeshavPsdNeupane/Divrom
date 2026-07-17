using UnityEngine;
using Kope.Core.Sensor;
using Kope.Component;
using Kope.Core.Identity;
using Kope.AI.AIBlackBoard;

[RequireComponent(typeof(CircleCollider2D))]
public class EntitySensor : SensorBase {
	[SerializeField, Range(0f, 10f), Tooltip("If the entity is inside the inner detection radius, " +
	"it will be considered a close target. And will be detected even if it's outside the field of view angle.")]
	private float innerDetectionRadius = 1f;
	[SerializeField, Range(0f, 360f)] private float fieldOfViewAngle = 90f;
	private Context contextNew;
	private FieldOfViewData _fieldOfViewData;
	private bool _fovDataInitialized;

	public FieldOfViewData FieldOfViewData {
		get {
			if (!this._fovDataInitialized) {
				this._fieldOfViewData = new FieldOfViewData(
					this.fieldOfViewAngle, this.detectionRadius,
					this.innerDetectionRadius);
				this._fovDataInitialized = true;
			}
			return this._fieldOfViewData;
		}
	}

	/// <summary>
	/// Pass the context from AIBrain
	/// </summary>
	/// <param name="context"></param>
	public void InitContextNew(Context contextNew) {
		this.contextNew = contextNew;
		contextNew.SetFieldOfViewData(this.FieldOfViewData);
	}

	void OnValidate() {
		this._fieldOfViewData = new FieldOfViewData(this.fieldOfViewAngle, this.detectionRadius, this.innerDetectionRadius);
	}

	public override void OnStart() {
		if (this.contextNew == null) {
			Debug.LogWarning($"[EntitySensor] Context is not assigned for {gameObject.name}. Please call InitContext with a valid Context instance before the sensor starts detecting." + this._parentGOHiearchPathMessage);
		}

	}

	public override void OnDetect(Collider2D other) {
		if (this.contextNew == null) {
			Debug.LogWarning($"[EntitySensor] Context is not assigned for {gameObject.name}. Cannot register detected entity." + this._parentGOHiearchPathMessage);
			return;
		}
		var entityInstance = other.GetComponentInParent<EntityInstanceNew>();
		if (entityInstance == null) return;
		this.contextNew.RegisterEntityContext(entityInstance.EntityDetail);
	}

	public override void OnDetectExit(Collider2D other) {
		if (this.contextNew == null) {
			Debug.LogWarning($"[EntitySensor] Context is not assigned for {gameObject.name}. Cannot remove detected entity." + this._parentGOHiearchPathMessage);
			return;
		}
		var entityInstance = other.GetComponentInParent<EntityInstanceNew>();

		if (entityInstance == null) return;
		this.contextNew.RemoveEntityContext(entityInstance.EntityDetail);
	}




	// int tempCounter = 0;
	// void Update() {
	// 	if (this.tempCounter != context.GetTotalEntityCount()) {
	// 		this.tempCounter = context.GetTotalEntityCount();
	// 		Debug.Log($"[EntitySensor] Total entities in context: {this.tempCounter}");
	// 		// this is just to verify that the sensor is properly 
	// 		// registering and removing entities from the context, and that the count is accurate.
	// 	}
	// }
}
