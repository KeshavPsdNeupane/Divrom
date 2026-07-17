

using UnityEngine;
using Kope.Core.Sensor;
using Kope.Component;
using Kope.Core.Identity;
using Kope.AI.Ctx;

[RequireComponent(typeof(CircleCollider2D))]
public class EntitySensor : SensorBase {
	[SerializeField, Range(0f, 10f), Tooltip("If the entity is inside the inner detection radius, " +
	"it will be considered a close target. And will be detected even if it's outside the field of view angle.")]
	private float innerDetectionRadius = 1f;
	[SerializeField, Range(0f, 360f)] private float fieldOfViewAngle = 90f;
	private Context context;
	private ContextNew contextNew;
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
	public void InitContext(Context context) {
		this.context = context;
		context.SetFieldOfViewData(this.FieldOfViewData);
	}

	public void InitContextNew(ContextNew contextNew) {
		this.contextNew = contextNew;
		contextNew.SetFieldOfViewData(this.FieldOfViewData);
	}


	int tempcount = 0;
	void Update() {
		int count = contextNew.TotalSizeOfDatabases();
		if (this.tempcount != count) {
			this.tempcount = count;
			Debug.Log($"[EntitySensor] Total entities in contextNew: {this.tempcount}");
		}
	}
	void OnValidate() {
		this._fieldOfViewData = new FieldOfViewData(this.fieldOfViewAngle, this.detectionRadius, this.innerDetectionRadius);
	}



	public override void OnStart() {
		if (this.context == null) {
			Debug.LogWarning($"[EntitySensor] Context is not assigned for {gameObject.name}. Please call InitContext with a valid Context instance before the sensor starts detecting." + this._parentGOHiearchPathMessage);
		}

	}

	public override void OnDetect(Collider2D other) {
		if (this.context == null) {
			Debug.LogWarning($"[EntitySensor] Context is not assigned for {gameObject.name}. Cannot register detected entity." + this._parentGOHiearchPathMessage);
			return;
		}
		var entityManager = other.GetComponentInParent<EntityInstance>();
		var entityInstance = other.GetComponentInParent<EntityInstanceNew>();

		if (entityManager == null && entityInstance == null) return;



		// this is garunteed to be valid for all entity since we check the commonname on the EI itself,
		// so we can skip the check here and just add it to the context
		// so if entity manager is valid then all other tags and registry should be valid as well,
		// if not then we have bigger problems and should just let it throw an error


		// later i am going to make this register an event like
		// this.OnEntityDetected?.Invoke(entityManager.EntityDetail) 
		// then brain wire the subcription to the context.
		if (entityManager != null)
			this.context.RegisterEntityContext(entityManager.EntityDetail);

		if (entityInstance != null)
			this.contextNew.RegisterEntityContext(entityInstance.EntityDetail);
	}

	public override void OnDetectExit(Collider2D other) {
		if (this.context == null) {
			Debug.LogWarning($"[EntitySensor] Context is not assigned for {gameObject.name}. Cannot remove detected entity." + this._parentGOHiearchPathMessage);
			return;
		}
		var entityManager = other.GetComponentInParent<EntityInstance>();
		var entityInstance = other.GetComponentInParent<EntityInstanceNew>();

		if (entityManager == null && entityInstance == null) return;

		if (entityManager != null)
			this.context.RemoveEntityContext(entityManager.EntityDetail);

		if (entityInstance != null)
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
